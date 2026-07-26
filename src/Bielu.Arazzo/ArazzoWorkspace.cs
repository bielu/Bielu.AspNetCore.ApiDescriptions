using Bielu.Arazzo.Models;

namespace Bielu.Arazzo;

/// <summary>
/// Registers the documents an <see cref="ArazzoDocument"/>'s <c>sourceDescriptions</c> point at, and
/// resolves <c>operationId</c>/<c>operationPath</c>/<c>channelPath</c>/<c>workflowId</c> references into
/// them. This is the seam PR 14's self-wiring plugs into: an ASP.NET Core app registers its own live
/// <c>AsyncApiDocument</c> and <c>OpenApiDocument</c> instances here so a step's reference can be
/// validated at startup instead of failing in production (see ARAZZO-PROPOSAL.md §3.B).
/// </summary>
public sealed class ArazzoWorkspace
{
    private readonly Dictionary<string, object> _documentsBySourceName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IArazzoSourceResolver> _resolversByType = new(StringComparer.Ordinal);

    public void RegisterResolver(IArazzoSourceResolver resolver) => _resolversByType[resolver.SourceType] = resolver;

    /// <summary>Registers the already-loaded document a <see cref="ArazzoSourceDescription"/> with this <paramref name="sourceName"/> points at.</summary>
    public void RegisterDocument(string sourceName, object document) => _documentsBySourceName[sourceName] = document;

    public bool TryGetDocument(string sourceName, out object? document) => _documentsBySourceName.TryGetValue(sourceName, out document);

    public bool TryGetResolver(string sourceType, out IArazzoSourceResolver? resolver) => _resolversByType.TryGetValue(sourceType, out resolver);

    /// <summary>
    /// Resolves a step's <see cref="ArazzoStep.OperationId"/> against the source description named
    /// <paramref name="sourceName"/>. Returns false if the source isn't registered or has no resolver for
    /// its declared type.
    /// </summary>
    public bool TryResolveOperation(string sourceName, string operationId, out System.Text.Json.Nodes.JsonNode? operation)
    {
        operation = null;
        if (!TryGetDocument(sourceName, out var document) || document is null)
        {
            return false;
        }

        foreach (var resolver in _resolversByType.Values)
        {
            if (resolver.TryResolveOperation(document, operationId, out operation))
            {
                return true;
            }
        }

        return false;
    }
}
