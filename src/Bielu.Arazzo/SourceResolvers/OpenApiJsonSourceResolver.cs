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
/// <c>/paths/~1pets/get</c>, also through a Path Item's <c>$ref</c>.
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

    /// <inheritdoc />
    public override string SourceType => ArazzoSourceDescriptionType.OpenApi.Value;

    /// <inheritdoc />
    public override bool TryResolveOperation(JsonNode document, string operationId, out JsonNode? operation)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(operationId);

        foreach (var section in new[] { "paths", "webhooks" })
        {
            if (document[section] is not JsonObject pathItems)
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

                foreach (var (method, candidate) in methods)
                {
                    if (Methods.Contains(method) &&
                        candidate?["operationId"] is JsonValue id &&
                        id.TryGetValue<string>(out var value) &&
                        string.Equals(value, operationId, StringComparison.Ordinal))
                    {
                        operation = candidate;
                        return true;
                    }
                }
            }
        }

        operation = null;
        return false;
    }

    /// <inheritdoc />
    public override bool TryResolveOperationPath(JsonNode document, string jsonPointer, out JsonNode? operation)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(jsonPointer);

        operation = null;
        // An HTTP method of a Path Item: one under paths or webhooks, or a reusable one in components.pathItems.
        // Any other object with a "get" key, such as a schema property, is no operation.
        var tokens = JsonDocumentPointer.Tokens(jsonPointer);
        if (tokens is not (["paths" or "webhooks", _, _] or ["components", "pathItems", _, _]) || !Methods.Contains(tokens[^1]))
        {
            return false;
        }

        // The method of the Path Item the rest of the pointer addresses, which may be a $ref to another.
        if (JsonDocumentPointer.EvaluateObject(document, tokens[..^1]) is { } pathItem
            && JsonDocumentPointer.FollowLocalReferences(document, pathItem)?[tokens[^1]] is JsonObject found)
        {
            operation = found;
        }

        return operation is not null;
    }
}
