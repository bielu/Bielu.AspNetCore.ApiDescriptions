using System.Text.Json.Nodes;

namespace Bielu.Arazzo;

/// <summary>
/// Resolves an <c>operationId</c>/<c>operationPath</c>/<c>channelPath</c>/<c>workflowId</c> reference
/// against one registered source document. Bielu.Arazzo.NET stays framework-free — it has no dependency
/// on ByteBard.AsyncAPI.NET or Microsoft.OpenApi — so it does not know how to look inside an OpenAPI or
/// AsyncAPI document itself. <c>Bielu.AspNetCore.Arazzo</c> (proposal PR 14) supplies the real resolvers,
/// one per <see cref="Models.ArazzoSourceDescription.Type"/>, and is where the self-wiring against an
/// app's own live AsyncAPI/OpenAPI documents happens.
/// </summary>
public interface IArazzoSourceResolver
{
    /// <summary>The source-description <c>type</c> this resolver handles: "openapi", "asyncapi", or "arazzo".</summary>
    string SourceType { get; }

    bool TryResolveOperation(object document, string operationId, out JsonNode? operation);

    bool TryResolveOperationPath(object document, string jsonPointer, out JsonNode? operation);

    bool TryResolveChannelPath(object document, string jsonPointer, out JsonNode? channel);
}
