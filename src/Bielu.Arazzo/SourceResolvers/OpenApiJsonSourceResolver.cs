using System.Text.Json.Nodes;
using Bielu.Arazzo.Models;

namespace Bielu.Arazzo.SourceResolvers;

/// <summary>
/// Resolves Arazzo <c>operationId</c>/<c>operationPath</c> references against an OpenAPI document read as JSON
/// (from a JSON or YAML file), with no dependency on an OpenAPI object model. This is the resolver to use when the
/// document is a file, such as one written at build time, rather than a live app's.
/// </summary>
/// <remarks>
/// An <c>operationId</c> is searched for in every operation under <c>paths</c> and <c>webhooks</c>, and in the
/// callbacks of those operations, following local <c>$ref</c>s of Path Items and callbacks. An <c>operationPath</c>
/// resolves when its pointer addresses an Operation Object: an HTTP method of a Path Item under <c>paths</c>,
/// <c>webhooks</c>, <c>components/pathItems</c> or <c>components/callbacks</c>, such as <c>/paths/~1pets/get</c>,
/// or one of its OpenAPI 3.2 <c>additionalOperations</c>, and from there into a callback's Path Item
/// (<c>…/post/callbacks/onEvent/{$request.body#~1url}/post</c>), also through <c>$ref</c>s.
/// </remarks>
/// <example>
/// <code>
/// var workspace = new ArazzoWorkspace();
/// workspace.RegisterResolver(new OpenApiJsonSourceResolver());
/// workspace.RegisterDocument("petStore", "openapi", JsonNode.Parse(File.ReadAllText("openapi.json"))!);
/// </code>
/// </example>
public sealed class OpenApiJsonSourceResolver : ArazzoSourceResolver<JsonNode>
{
    private static readonly HashSet<string> Methods = new(StringComparer.Ordinal)
    {
        "get", "put", "post", "delete", "options", "head", "patch", "trace", "query",
    };

    private const string AdditionalOperations = "additionalOperations";
    private const string Callbacks = "callbacks";

    /// <inheritdoc />
    public override string SourceType => ArazzoSourceDescriptionType.OpenApi.Value;

    /// <inheritdoc />
    public override bool TryResolveOperation(JsonNode document, string operationId, out JsonNode? operation)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(operationId);

        operation = null;
        if (document is not JsonObject root)
        {
            return false;
        }

        // Every Path Item is visited once, so a callback that leads back to a Path Item already seen ends there.
        var visited = new HashSet<JsonObject>(ReferenceEqualityComparer.Instance);
        foreach (var section in new[] { "paths", "webhooks" })
        {
            if (root[section] is not JsonObject pathItems)
            {
                continue;
            }

            foreach (var (key, pathItem) in pathItems)
            {
                if (!IsExtension(key)
                    && pathItem is JsonObject item
                    && Operations(root, item, visited).FirstOrDefault(candidate => HasOperationId(candidate, operationId)) is { } found)
                {
                    operation = found;
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// A Path Item's Operation Objects (its HTTP methods and the ones OpenAPI 3.2 keys under
    /// <c>additionalOperations</c>), then, depth first, the operations of their callbacks' Path Items. Local
    /// <c>$ref</c>s of Path Items and callbacks are followed; a value that is not an object, as in a malformed
    /// document, is skipped.
    /// </summary>
    private static IEnumerable<JsonObject> Operations(JsonNode root, JsonObject pathItem, HashSet<JsonObject> visited)
    {
        // OpenAPI 3.1 lets a Path Item be a $ref, such as to #/components/pathItems/Pets.
        if (JsonDocumentPointer.FollowLocalReferences(root, pathItem) is not { } item || !visited.Add(item))
        {
            yield break;
        }

        var operations = new List<JsonObject>();
        foreach (var (method, candidate) in item)
        {
            if (Methods.Contains(method) && candidate is JsonObject operation)
            {
                operations.Add(operation);
            }
        }

        if (item[AdditionalOperations] is JsonObject additional)
        {
            operations.AddRange(additional.Select(pair => pair.Value).OfType<JsonObject>());
        }

        foreach (var operation in operations)
        {
            yield return operation;
            if (operation[Callbacks] is not JsonObject callbacks)
            {
                continue;
            }

            foreach (var (_, callback) in callbacks)
            {
                if (callback is not JsonObject entry || JsonDocumentPointer.FollowLocalReferences(root, entry) is not { } resolved)
                {
                    continue;
                }

                foreach (var (expression, callbackPathItem) in resolved)
                {
                    if (!IsExtension(expression) && callbackPathItem is JsonObject inner)
                    {
                        foreach (var nested in Operations(root, inner, visited))
                        {
                            yield return nested;
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Whether a key of a Paths or Callback Object is a specification extension (<c>x-…</c>) rather than a path or
    /// a callback expression: its value may be any object, which is no Path Item.
    /// </summary>
    private static bool IsExtension(string key) => key.StartsWith("x-", StringComparison.Ordinal);

    private static bool HasOperationId(JsonObject operation, string operationId) =>
        operation["operationId"] is JsonValue id &&
        id.TryGetValue<string>(out var value) &&
        string.Equals(value, operationId, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool TryResolveOperationPath(JsonNode document, string jsonPointer, out JsonNode? operation)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(jsonPointer);

        operation = null;
        if (JsonDocumentPointer.Tokens(jsonPointer) is not { } tokens)
        {
            return false;
        }

        // Where a Path Item may start: under paths or webhooks, reusable in components.pathItems, or inside a
        // reusable callback in components.callbacks. Any other object with a "get" key, such as a schema
        // property, is no operation.
        var start = tokens switch
        {
            ["paths", var path, ..] when !IsExtension(path) => 2,
            ["webhooks", _, ..] => 2,
            ["components", "pathItems", _, ..] => 3,
            ["components", Callbacks, _, var expression, ..] when !IsExtension(expression) => 4,
            _ => 0,
        };
        if (start == 0 || JsonDocumentPointer.EvaluateObject(document, tokens[..start]) is not { } pathItem)
        {
            return false;
        }

        operation = OperationIn(document, pathItem, tokens[start..]);
        return operation is not null;
    }

    /// <summary>
    /// The operation <paramref name="rest"/> names in <paramref name="pathItem"/>: an HTTP method, or
    /// <c>additionalOperations/{method}</c>, optionally followed by <c>callbacks/{name}/{expression}</c> and the
    /// same again in that callback's Path Item. Path Items and callbacks may be local <c>$ref</c>s.
    /// </summary>
    private static JsonObject? OperationIn(JsonNode root, JsonObject pathItem, ReadOnlySpan<string> rest)
    {
        // Each step goes one Path Item deeper, so the pointer's own length bounds the walk: no cycle can loop it.
        while (true)
        {
            if (JsonDocumentPointer.FollowLocalReferences(root, pathItem) is not { } item)
            {
                return null;
            }

            JsonObject? operation;
            int used;
            if (rest.Length >= 1 && Methods.Contains(rest[0]))
            {
                operation = item[rest[0]] as JsonObject;
                used = 1;
            }
            else if (rest.Length >= 2 && rest[0] == AdditionalOperations)
            {
                operation = (item[AdditionalOperations] as JsonObject)?[rest[1]] as JsonObject;
                used = 2;
            }
            else
            {
                return null;
            }

            rest = rest[used..];
            if (operation is null || rest.Length == 0)
            {
                return operation;
            }

            if (rest.Length < 4
                || rest[0] != Callbacks
                || IsExtension(rest[2])
                || (operation[Callbacks] as JsonObject)?[rest[1]] is not JsonObject callback
                || JsonDocumentPointer.FollowLocalReferences(root, callback)?[rest[2]] is not JsonObject next)
            {
                return null;
            }

            pathItem = next;
            rest = rest[3..];
        }
    }
}
