using System.Text.Json.Nodes;
using Bielu.Arazzo;
using Bielu.Arazzo.Models;
using Microsoft.OpenApi;

namespace Bielu.AspNetCore.Arazzo.SourceResolvers;

/// <summary>
/// Resolves Arazzo <c>operationId</c>/<c>operationPath</c> references against a live <see cref="OpenApiDocument"/>.
/// Registered for <see cref="ArazzoSourceDescriptionType.OpenApi"/> sources by <c>AddArazzo</c>'s self-wiring.
/// </summary>
public sealed class OpenApiSourceResolver : ArazzoSourceResolver<OpenApiDocument>
{
    /// <inheritdoc />
    public override string SourceType => ArazzoSourceDescriptionType.OpenApi;

    /// <inheritdoc />
    public override bool TryResolveOperation(OpenApiDocument document, string operationId, out JsonNode? operation)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(operationId);

        operation = null;
        foreach (var pathItem in document.Paths.Values)
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
    public override bool TryResolveOperationPath(OpenApiDocument document, string jsonPointer, out JsonNode? operation)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(jsonPointer);

        operation = null;
        if (!TryParsePathPointer(jsonPointer, out var path, out var method))
        {
            return false;
        }

        if (!document.Paths.TryGetValue(path, out var pathItem) ||
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
        if (lastSlash < 0 || !JsonPointerSegment.TryUnescape(remainder[..lastSlash], out path))
        {
            return false;
        }

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

    private static JsonNode? Serialize(OpenApiOperation operation)
    {
        using var stringWriter = new StringWriter();
        var writer = new OpenApiJsonWriter(stringWriter);
        operation.SerializeAsV3(writer);
        return JsonNode.Parse(stringWriter.ToString());
    }
}
