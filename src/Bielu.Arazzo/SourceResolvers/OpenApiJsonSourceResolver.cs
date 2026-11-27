using System.Text.Json.Nodes;
using Bielu.Arazzo.Models;

namespace Bielu.Arazzo.SourceResolvers;

/// <summary>
/// Resolves Arazzo <c>operationId</c>/<c>operationPath</c> references against an OpenAPI document read as JSON
/// (from a JSON or YAML file), with no dependency on an OpenAPI object model. This is the resolver to use when the
/// document is a file, such as one written at build time, rather than a live app's.
/// </summary>
/// <remarks>
/// An <c>operationId</c> is searched for in every operation under <c>paths</c> and <c>webhooks</c>. An
/// <c>operationPath</c> resolves when its pointer addresses an object, such as <c>/paths/~1pets/get</c>.
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
                if (pathItem is not JsonObject methods)
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

        operation = JsonDocumentPointer.EvaluateObject(document, jsonPointer);
        return operation is not null;
    }
}
