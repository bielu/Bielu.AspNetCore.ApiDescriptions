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
    private readonly Dictionary<string, (string SourceType, object Document)> _documentsBySourceName =
        new(StringComparer.Ordinal);

    private readonly Dictionary<string, IArazzoSourceResolver> _resolversByType = new(StringComparer.Ordinal);

    /// <summary>
    /// Registers a resolver for one source-description <c>type</c> (see <see cref="IArazzoSourceResolver.SourceType"/>).
    /// </summary>
    /// <example>
    /// <code>
    /// workspace.RegisterResolver(new OpenApiSourceResolver());
    /// workspace.RegisterResolver(new AsyncApiSourceResolver());
    /// </code>
    /// </example>
    public void RegisterResolver(IArazzoSourceResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        _resolversByType[resolver.SourceType] = resolver;
    }

    /// <summary>
    /// Registers the already-loaded document an <see cref="ArazzoSourceDescription"/> with this
    /// <paramref name="sourceName"/> points at, together with its declared <paramref name="sourceType"/>
    /// (e.g. "openapi", "asyncapi", or "arazzo") so <see cref="TryResolveOperation"/> can pick the right resolver.
    /// </summary>
    public void RegisterDocument(string sourceName, string sourceType, object document)
    {
        ArgumentNullException.ThrowIfNull(sourceName);
        ArgumentNullException.ThrowIfNull(sourceType);
        ArgumentNullException.ThrowIfNull(document);
        _documentsBySourceName[sourceName] = (sourceType, document);
    }

    /// <summary>Looks up the document registered for <paramref name="sourceName"/>. Returns false if none was registered.</summary>
    public bool TryGetDocument(string sourceName, out object? document)
    {
        ArgumentNullException.ThrowIfNull(sourceName);

        if (_documentsBySourceName.TryGetValue(sourceName, out var entry))
        {
            document = entry.Document;
            return true;
        }

        document = null;
        return false;
    }

    /// <summary>Looks up the resolver registered for <paramref name="sourceType"/>. Returns false if none was registered.</summary>
    public bool TryGetResolver(string sourceType, out IArazzoSourceResolver? resolver)
    {
        ArgumentNullException.ThrowIfNull(sourceType);

        return _resolversByType.TryGetValue(sourceType, out resolver);
    }

    /// <summary>
    /// Resolves a step's <see cref="ArazzoStep.OperationId"/> against the source description named
    /// <paramref name="sourceName"/>, using the resolver registered for that source's declared type.
    /// Returns false if the source isn't registered or has no resolver for its declared type.
    /// </summary>
    public bool TryResolveOperation(string sourceName, string operationId,
        out System.Text.Json.Nodes.JsonNode? operation)
    {
        ArgumentNullException.ThrowIfNull(sourceName);
        ArgumentNullException.ThrowIfNull(operationId);

        operation = null;
        if (!_documentsBySourceName.TryGetValue(sourceName, out var entry))
        {
            return false;
        }

        return _resolversByType.TryGetValue(entry.SourceType, out var resolver)
               && resolver.TryResolveOperation(entry.Document, operationId, out operation);
    }

    /// <summary>
    /// Resolves a step's <see cref="ArazzoStep.OperationPath"/> JSON Pointer against the source description
    /// named <paramref name="sourceName"/>, using the resolver registered for that source's declared type.
    /// Returns false if the source isn't registered or has no resolver for its declared type.
    /// </summary>
    public bool TryResolveOperationPath(string sourceName, string jsonPointer,
        out System.Text.Json.Nodes.JsonNode? operation)
    {
        ArgumentNullException.ThrowIfNull(sourceName);
        ArgumentNullException.ThrowIfNull(jsonPointer);

        operation = null;
        if (!_documentsBySourceName.TryGetValue(sourceName, out var entry))
        {
            return false;
        }

        return _resolversByType.TryGetValue(entry.SourceType, out var resolver)
               && resolver.TryResolveOperationPath(entry.Document, jsonPointer, out operation);
    }

    /// <summary>
    /// Resolves a step's <see cref="ArazzoStep.ChannelPath"/> JSON Pointer against the source description
    /// named <paramref name="sourceName"/>, using the resolver registered for that source's declared type.
    /// Returns false if the source isn't registered or has no resolver for its declared type.
    /// </summary>
    public bool TryResolveChannelPath(string sourceName, string jsonPointer,
        out System.Text.Json.Nodes.JsonNode? channel)
    {
        ArgumentNullException.ThrowIfNull(sourceName);
        ArgumentNullException.ThrowIfNull(jsonPointer);

        channel = null;
        if (!_documentsBySourceName.TryGetValue(sourceName, out var entry))
        {
            return false;
        }

        return _resolversByType.TryGetValue(entry.SourceType, out var resolver)
               && resolver.TryResolveChannelPath(entry.Document, jsonPointer, out channel);
    }
}
