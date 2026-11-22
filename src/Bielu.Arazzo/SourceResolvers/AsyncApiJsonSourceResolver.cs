using System.Text.Json.Nodes;
using Bielu.Arazzo.Models;

namespace Bielu.Arazzo.SourceResolvers;

/// <summary>
/// Resolves Arazzo <c>operationId</c>/<c>operationPath</c>/<c>channelPath</c> references against an AsyncAPI
/// document read as JSON (from a JSON or YAML file), with no dependency on an AsyncAPI object model. This is the
/// resolver to use when the document is a file, such as one written at build time, rather than a live app's.
/// </summary>
/// <remarks>
/// An <c>operationId</c> is an AsyncAPI 3 operation's key under <c>operations</c>, or, in an AsyncAPI 2 document,
/// the <c>operationId</c> of a channel's <c>publish</c> or <c>subscribe</c> operation. A pointer resolves when it
/// addresses an object in the document.
/// </remarks>
/// <example>
/// <code>
/// var workspace = new ArazzoWorkspace();
/// workspace.RegisterResolver(new AsyncApiJsonSourceResolver());
/// workspace.RegisterDocument("events", "asyncapi", JsonNode.Parse(File.ReadAllText("asyncapi.json"))!);
/// </code>
/// </example>
public sealed class AsyncApiJsonSourceResolver : ArazzoSourceResolver<JsonNode>
{
    private static readonly string[] Version2OperationKeys = ["publish", "subscribe"];

    /// <inheritdoc />
    public override string SourceType => ArazzoSourceDescriptionType.AsyncApi.Value;

    /// <inheritdoc />
    public override bool TryResolveOperation(JsonNode document, string operationId, out JsonNode? operation)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(operationId);

        if (document["operations"] is JsonObject operations &&
            operations.TryGetPropertyValue(operationId, out var candidate) && candidate is JsonObject)
        {
            operation = candidate;
            return true;
        }

        if (document["channels"] is JsonObject channels)
        {
            foreach (var (_, channel) in channels)
            {
                foreach (var key in Version2OperationKeys)
                {
                    if (channel?[key] is JsonObject version2Operation &&
                        version2Operation["operationId"] is JsonValue id &&
                        id.TryGetValue<string>(out var value) &&
                        string.Equals(value, operationId, StringComparison.Ordinal))
                    {
                        operation = version2Operation;
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

    /// <inheritdoc />
    public override bool TryResolveChannelPath(JsonNode document, string jsonPointer, out JsonNode? channel)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(jsonPointer);

        channel = JsonDocumentPointer.EvaluateObject(document, jsonPointer);
        return channel is not null;
    }
}
