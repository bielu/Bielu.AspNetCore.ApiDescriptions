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
/// the <c>operationId</c> of a channel's <c>publish</c> or <c>subscribe</c> operation. An <c>operationPath</c>
/// resolves when it addresses an operation (<c>/operations/onSignup</c>, <c>/components/operations/…</c>, or an
/// AsyncAPI 2 channel's <c>/channels/orders/publish</c>), and a <c>channelPath</c> when it addresses a channel
/// (<c>/channels/…</c> or <c>/components/channels/…</c>).
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

        operation = null;
        if (document is not JsonObject root)
        {
            return false;
        }

        if (root["operations"] is JsonObject operations &&
            operations.TryGetPropertyValue(operationId, out var candidate) && candidate is JsonObject found &&
            JsonDocumentPointer.FollowLocalReferences(root, found) is { } resolved)
        {
            operation = resolved;
            return true;
        }

        if (root["channels"] is JsonObject channels)
        {
            foreach (var (_, channel) in channels)
            {
                if (channel is not JsonObject item || JsonDocumentPointer.FollowLocalReferences(root, item) is not { } resolvedChannel)
                {
                    continue;
                }

                foreach (var key in Version2OperationKeys)
                {
                    if (resolvedChannel[key] is JsonObject version2Operation &&
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

        var tokens = JsonDocumentPointer.Tokens(jsonPointer);
        var isOperation = tokens switch
        {
            ["operations", _] or ["components", "operations", _] => true,
            ["channels", _, "publish" or "subscribe"] or ["components", "channels", _, "publish" or "subscribe"] => true,
            _ => false,
        };
        operation = isOperation ? Target(document, tokens!) : null;
        return operation is not null;
    }

    /// <inheritdoc />
    public override bool TryResolveChannelPath(JsonNode document, string jsonPointer, out JsonNode? channel)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(jsonPointer);

        var tokens = JsonDocumentPointer.Tokens(jsonPointer);
        var isChannel = tokens is ["channels", _] or ["components", "channels", _];
        channel = isChannel ? Target(document, tokens!) : null;
        return channel is not null;
    }

    /// <summary>
    /// The object the tokens address, with local <c>$ref</c>s on the channel and the operation followed, such as a
    /// channel that is <c>{ "$ref": "#/components/channels/orders" }</c>; <see langword="null"/> when a reference
    /// points at nothing, outside the document, or back into its chain.
    /// </summary>
    private static JsonObject? Target(JsonNode document, string[] tokens)
    {
        // An AsyncAPI 2 operation sits inside its channel, which may itself be a $ref: resolve that first.
        var (channelTokens, rest) = tokens is [.., "publish" or "subscribe"] ? (tokens[..^1], tokens[^1..]) : (tokens, []);
        if (JsonDocumentPointer.EvaluateObject(document, channelTokens) is not { } target
            || JsonDocumentPointer.FollowLocalReferences(document, target) is not { } resolved)
        {
            return null;
        }

        if (rest.Length == 0)
        {
            return resolved;
        }

        return JsonDocumentPointer.EvaluateObject(resolved, rest) is { } inner
            ? JsonDocumentPointer.FollowLocalReferences(document, inner)
            : null;
    }
}
