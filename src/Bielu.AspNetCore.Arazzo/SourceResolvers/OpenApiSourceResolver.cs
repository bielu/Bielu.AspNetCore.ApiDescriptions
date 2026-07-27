using System.Text.Json.Nodes;
using Bielu.Arazzo;
using Bielu.Arazzo.Models;
using Microsoft.OpenApi;

namespace Bielu.AspNetCore.Arazzo.SourceResolvers;

/// <summary>
/// Resolves Arazzo <c>operationId</c>/<c>operationPath</c> references against a live <see cref="OpenApiDocument"/>.
/// Registered for <see cref="ArazzoSourceDescriptionType.OpenApi"/> sources by <c>AddArazzo</c>'s self-wiring.
/// </summary>
public sealed class OpenApiSourceResolver : IArazzoSourceResolver
{
    /// <inheritdoc />
    public string SourceType => ArazzoSourceDescriptionType.OpenApi;

    /// <inheritdoc />
    public bool TryResolveOperation(object document, string operationId, out JsonNode? operation)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(operationId);

        operation = null;
        if (document is not OpenApiDocument openApiDocument)
        {
            return false;
        }

        foreach (var pathItem in openApiDocument.Paths.Values)
        {
            if (pathItem is not OpenApiPathItem { Operations: not null } concretePathItem)
            {
                continue;
            }

            foreach (var candidate in concretePathItem.Operations.Values)
            {
                if (string.Equals(candidate.OperationId, operationId, StringComparison.Ordinal))
                {
                    operation = Serialize(candidate);
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Resolves an <c>/paths/{path}/{method}</c> JSON Pointer, as produced by <c>ArazzoStepBuilder.OperationPath</c>.</summary>
    public bool TryResolveOperationPath(object document, string jsonPointer, out JsonNode? operation)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(jsonPointer);

        operation = null;
        if (document is not OpenApiDocument openApiDocument ||
            !TryParsePathPointer(jsonPointer, out var path, out var method))
        {
            return false;
        }

        if (!openApiDocument.Paths.TryGetValue(path, out var pathItem) ||
            pathItem is not OpenApiPathItem { Operations: not null } concretePathItem)
        {
            return false;
        }

        if (!concretePathItem.Operations.TryGetValue(method, out var candidate))
        {
            return false;
        }

        operation = Serialize(candidate);
        return true;
    }

    /// <summary>OpenAPI documents have no channels; always returns false.</summary>
    public bool TryResolveChannelPath(object document, string jsonPointer, out JsonNode? channel)
    {
        channel = null;
        return false;
    }

    private static bool TryParsePathPointer(string jsonPointer, out string path, out HttpMethod method)
    {
        path = string.Empty;
        method = HttpMethod.Get;

        const string prefix = "/paths/";
        if (!jsonPointer.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var remainder = jsonPointer[prefix.Length..];
        var lastSlash = remainder.LastIndexOf('/');
        if (lastSlash < 0)
        {
            return false;
        }

        path = UnescapePointerSegment(remainder[..lastSlash]);
        try
        {
            method = HttpMethod.Parse(remainder.AsSpan(lastSlash + 1));
        }
        catch (Exception)
        {
            return false;
        }

        return true;
    }

    private static string UnescapePointerSegment(string segment) => segment.Replace("~1", "/").Replace("~0", "~");

    private static JsonNode? Serialize(OpenApiOperation operation)
    {
        using var stringWriter = new StringWriter();
        var writer = new OpenApiJsonWriter(stringWriter);
        operation.SerializeAsV3(writer);
        return JsonNode.Parse(stringWriter.ToString());
    }
}
