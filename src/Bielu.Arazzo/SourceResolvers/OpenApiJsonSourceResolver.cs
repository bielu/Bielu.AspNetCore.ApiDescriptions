using System.Text.Json.Nodes;
using Bielu.Arazzo.Models;

namespace Bielu.Arazzo.SourceResolvers;

/// <summary>
/// Resolves Arazzo <c>operationId</c>/<c>operationPath</c> references against an OpenAPI document read as JSON
/// (from a JSON or YAML file), with no dependency on an OpenAPI object model. This is the resolver to use when the
/// document is a file, such as one written at build time, rather than a live app's.
/// </summary>
/// <remarks>
/// An <c>operationId</c> is searched for in every operation under <c>paths</c> and <c>webhooks</c>, following a
/// Path Item's local <c>$ref</c>. An <c>operationPath</c> resolves when its pointer addresses an Operation Object:
/// an HTTP method of a Path Item under <c>paths</c>, <c>webhooks</c> or <c>components/pathItems</c>, such as
/// <c>/paths/~1pets/get</c>, or one of its OpenAPI 3.2 <c>additionalOperations</c>, also through a Path Item's
/// <c>$ref</c>.
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

    /// <inheritdoc />
    public override string SourceType => ArazzoSourceDescriptionType.OpenApi.Value;

    /// <inheritdoc />
    public override bool TryResolveOperation(JsonNode document, string operationId, out JsonNode? operation)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(operationId);

        if (document is not JsonObject root)
        {
            operation = null;
            return false;
        }

        foreach (var section in new[] { "paths", "webhooks" })
        {
            if (root[section] is not JsonObject pathItems)
            {
                continue;
            }

            foreach (var (_, pathItem) in pathItems)
            {
                // OpenAPI 3.1 lets a Path Item be a $ref, such as to #/components/pathItems/Pets.
                if (pathItem is not JsonObject item || JsonDocumentPointer.FollowLocalReferences(document, item) is not { } methods)
                {
                    continue;
                }

                if (Operations(methods).FirstOrDefault(candidate => HasOperationId(candidate, operationId)) is { } found)
                {
                    operation = found;
                    return true;
                }
            }
        }

        operation = null;
        return false;
    }

    /// <summary>
    /// A Path Item's Operation Objects: its HTTP methods, and the ones OpenAPI 3.2 keys by method name under
    /// <c>additionalOperations</c>. A value that is not an object, as in a malformed document, is skipped.
    /// </summary>
    private static IEnumerable<JsonObject> Operations(JsonObject pathItem)
    {
        foreach (var (method, candidate) in pathItem)
        {
            if (Methods.Contains(method) && candidate is JsonObject operation)
            {
                yield return operation;
            }
        }

        if (pathItem[AdditionalOperations] is JsonObject additional)
        {
            foreach (var (_, candidate) in additional)
            {
                if (candidate is JsonObject operation)
                {
                    yield return operation;
                }
            }
        }
    }

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
        // An HTTP method of a Path Item: one under paths or webhooks, or a reusable one in components.pathItems.
        // Any other object with a "get" key, such as a schema property, is no operation.
        // OpenAPI 3.2 keys any other method under the Path Item's additionalOperations, such as .../additionalOperations/COPY.
        var tokens = JsonDocumentPointer.Tokens(jsonPointer);
        var (pathItemTokens, keys) = tokens switch
        {
            ["paths" or "webhooks", _, var method] when Methods.Contains(method) => (tokens[..2], new[] { method }),
            ["components", "pathItems", _, var method] when Methods.Contains(method) => (tokens[..3], new[] { method }),
            ["paths" or "webhooks", _, AdditionalOperations, var method] => (tokens[..2], new[] { AdditionalOperations, method }),
            ["components", "pathItems", _, AdditionalOperations, var method] => (tokens[..3], new[] { AdditionalOperations, method }),
            _ => ((string[]?)null, (string[]?)null),
        };
        if (pathItemTokens is null || keys is null)
        {
            return false;
        }

        // The operation in the Path Item the pointer starts with, which may be a $ref to another.
        if (JsonDocumentPointer.EvaluateObject(document, pathItemTokens) is { } pathItem
            && JsonDocumentPointer.FollowLocalReferences(document, pathItem) is { } resolved
            && JsonDocumentPointer.EvaluateObject(resolved, keys) is { } found)
        {
            operation = found;
        }

        return operation is not null;
    }
}
