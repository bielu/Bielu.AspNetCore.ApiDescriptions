using System.Text.Json.Nodes;

namespace Bielu.Arazzo;

/// <summary>
/// Resolves an <c>operationId</c>/<c>operationPath</c>/<c>channelPath</c> reference against one registered
/// source document. Bielu.Arazzo.NET stays framework-free, with no dependency on ByteBard.AsyncAPI.NET or
/// Microsoft.OpenApi, so it does not know how to look inside an OpenAPI or AsyncAPI document itself.
/// <c>Bielu.AspNetCore.Arazzo</c> supplies the real resolvers, one per <see cref="Models.ArazzoSourceDescription.Type"/>.
/// </summary>
/// <remarks>
/// This non-generic form lets <see cref="ArazzoWorkspace"/> hold resolvers for different document types side by
/// side. Implement a resolver by deriving from <see cref="ArazzoSourceResolver{TDocument}"/>, which checks the
/// document's type once and hands its methods the typed document.
/// </remarks>
public interface IArazzoSourceResolver
{
    /// <summary>The source-description <c>type</c> this resolver handles: "openapi", "asyncapi", or "arazzo".</summary>
    string SourceType { get; }

    /// <summary>The type of document this resolver reads, such as <c>AsyncApiDocument</c>.</summary>
    Type DocumentType { get; }

    /// <summary>Attempts to resolve an operation by its ID; returns false if the reference cannot be resolved against the given document.</summary>
    /// <exception cref="ArgumentException"><paramref name="document"/> is not a <see cref="DocumentType"/>.</exception>
    bool TryResolveOperation(object document, string operationId, out JsonNode? operation);

    /// <summary>Attempts to resolve an operation by its JSON pointer path; returns false if the reference cannot be resolved against the given document.</summary>
    /// <exception cref="ArgumentException"><paramref name="document"/> is not a <see cref="DocumentType"/>.</exception>
    bool TryResolveOperationPath(object document, string jsonPointer, out JsonNode? operation);

    /// <summary>Attempts to resolve a channel by its JSON pointer path; returns false if the reference cannot be resolved against the given document.</summary>
    /// <exception cref="ArgumentException"><paramref name="document"/> is not a <see cref="DocumentType"/>.</exception>
    bool TryResolveChannelPath(object document, string jsonPointer, out JsonNode? channel);
}
