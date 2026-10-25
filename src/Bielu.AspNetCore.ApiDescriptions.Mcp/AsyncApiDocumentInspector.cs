using System.Text.Json.Nodes;

namespace Bielu.AspNetCore.ApiDescriptions.Mcp;

/// <summary>A message an AsyncAPI operation refers to.</summary>
/// <param name="Name">The name the operation knows it by: the key it is referenced under, such as a channel's message alias.</param>
/// <param name="Pointer">
/// The canonical pointer of the message itself, once every <c>$ref</c> on the way is followed: the component for a
/// message defined under <c>components/messages</c>, the channel entry for an inline one.
/// </param>
/// <param name="Node">The message, or <see langword="null"/> when <paramref name="Problem"/> says why it could not be found.</param>
/// <param name="Problem">Why the reference to the message could not be followed, if it could not.</param>
internal sealed record AsyncApiMessageReference(string Name, string Pointer, JsonObject? Node, string? Problem);

/// <summary>An operation of an AsyncAPI document, 2.x or 3.x, with its channel and messages located.</summary>
/// <param name="Id">The operation's key (3.x) or <c>operationId</c> (2.x), else <c>"{verb} {address}"</c>.</param>
/// <param name="Pointer">The canonical pointer of the operation.</param>
/// <param name="Action">
/// <c>send</c> or <c>receive</c>, always from the point of view of the documented application. A 2.x
/// <c>subscribe</c> operation is one the application sends; a 2.x <c>publish</c> one it receives.
/// </param>
/// <param name="Node">The operation as written.</param>
/// <param name="ChannelKey">The channel's key under <c>channels</c>.</param>
/// <param name="ChannelPointer">The canonical pointer of the channel.</param>
/// <param name="Channel">The channel, when it could be found.</param>
/// <param name="Address">The channel's address; in 2.x, its key.</param>
/// <param name="Messages">The messages the operation refers to.</param>
internal sealed record AsyncApiOperation(
    string Id,
    string Pointer,
    string Action,
    JsonObject Node,
    string? ChannelKey,
    string? ChannelPointer,
    JsonObject? Channel,
    string? Address,
    IReadOnlyList<AsyncApiMessageReference> Messages);

/// <summary>A server an operation can be reached on.</summary>
/// <param name="Name">The server's key under <c>servers</c>.</param>
/// <param name="Pointer">The server's canonical pointer.</param>
/// <param name="Node">The server, or <see langword="null"/> when the reference to it is broken.</param>
internal sealed record AsyncApiServer(string Name, string Pointer, JsonObject? Node);

/// <summary>
/// Answers questions about a serialized AsyncAPI document (2.x or 3.x) in a shape that is short enough for an agent's
/// context: its operations, and one message with every part of the document it references.
/// </summary>
internal static class AsyncApiDocumentInspector
{
    /// <summary>Finds every operation, in document order.</summary>
    public static IReadOnlyList<AsyncApiOperation> GetOperations(JsonObject document) =>
        IsAsyncApi2(document) ? GetOperations2(document) : GetOperations3(document);

    /// <summary>Finds an operation by its id, exactly or else ignoring case.</summary>
    public static AsyncApiOperation? FindOperation(JsonObject document, string id)
    {
        var operations = GetOperations(document);
        return operations.FirstOrDefault(operation => string.Equals(operation.Id, id, StringComparison.Ordinal))
               ?? operations.FirstOrDefault(operation => string.Equals(operation.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Summarizes the operations: their id, whether the app sends or receives, the channel and its address, the
    /// messages (each by its name and canonical pointer), and the summary.
    /// </summary>
    public static JsonArray ListOperations(JsonObject document) =>
        new(GetOperations(document).Select(operation => (JsonNode?)Summarize(operation)).ToArray());

    /// <summary>
    /// The servers an operation can be reached on: those its channel lists, or every server of the document when the
    /// channel lists none, as AsyncAPI specifies.
    /// </summary>
    public static IReadOnlyList<AsyncApiServer> GetServers(JsonObject document, AsyncApiOperation operation, out bool implicitServers)
    {
        var listed = (operation.Channel?["servers"] as JsonArray ?? [])
            .Select(server => IsAsyncApi2(document)
                ? String(server) is { } name ? JsonPointer.Build("servers", name) : null
                : JsonPointer.ReferenceOf(server) is { } reference ? JsonPointer.Normalize(reference) : null)
            .OfType<string>()
            .ToList();

        implicitServers = listed.Count == 0;
        if (implicitServers)
        {
            listed = (document["servers"] as JsonObject ?? []).Select(entry => JsonPointer.Build("servers", entry.Key)).ToList();
        }

        return listed
            .Select(pointer =>
            {
                var (_, node, _) = JsonPointer.Follow(document, pointer);
                return new AsyncApiServer(LastSegment(pointer) ?? pointer, pointer, node);
            })
            .ToList();
    }

    /// <summary>
    /// Returns one message with every part of the document it references, or why it could not.
    /// </summary>
    /// <param name="document">The AsyncAPI document.</param>
    /// <param name="message">
    /// The message's canonical pointer (as <see cref="ListOperations"/> returns it), its key under
    /// <c>components/messages</c>, or the name an operation or channel knows it by, matched ignoring case.
    /// </param>
    public static (JsonObject? Message, string? Error) GetMessage(JsonObject document, string message)
    {
        var (name, pointer, node, error) = LocateMessage(document, message);
        if (node is null)
        {
            return (null, error);
        }

        var closure = new ReferenceClosure(document);
        closure.Walk(node);

        var result = new JsonObject
        {
            ["name"] = name,
            ["pointer"] = pointer,
            ["message"] = node.DeepClone(),
            ["references"] = References(closure.References),
        };
        if (closure.Unresolved.Count > 0)
        {
            result["unresolved"] = Unresolved(closure.Unresolved);
        }

        return (result, null);
    }

    /// <summary>
    /// The names agents may know the messages by: their keys under <c>components/messages</c> and the names operations
    /// refer to them with.
    /// </summary>
    public static IEnumerable<string> MessageNames(JsonObject document) =>
        (document["components"]?["messages"] as JsonObject ?? []).Select(entry => entry.Key)
        .Concat(MessageCandidates(document).Select(candidate => candidate.Name))
        .Distinct(StringComparer.Ordinal);

    internal static bool IsAsyncApi2(JsonObject document) =>
        String(document["asyncapi"])?.StartsWith("2.", StringComparison.Ordinal) == true;

    /// <summary>The referenced nodes as one object keyed by canonical pointer.</summary>
    internal static JsonObject References(IEnumerable<KeyValuePair<string, JsonNode>> references)
    {
        var result = new JsonObject();
        foreach (var (pointer, node) in references)
        {
            result[pointer] = node.DeepClone();
        }

        return result;
    }

    /// <summary>The references that could not be followed, as <c>[{ "ref", "problem" }]</c>.</summary>
    internal static JsonArray Unresolved(IReadOnlyDictionary<string, string> unresolved) =>
        new(unresolved.Select(entry => (JsonNode?)new JsonObject { ["ref"] = entry.Key, ["problem"] = entry.Value }).ToArray());

    /// <summary>The unescaped last segment of a local reference.</summary>
    internal static string? LastSegment(string? reference) =>
        reference is not null && JsonPointer.TryParse(reference, out var segments) && segments.Count > 0 ? segments[^1] : null;

    internal static string? String(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) && text.Length > 0 ? text : null;

    private static JsonObject Summarize(AsyncApiOperation operation)
    {
        var summary = new JsonObject
        {
            ["id"] = operation.Id,
            ["action"] = operation.Action,
            ["channel"] = operation.ChannelKey,
            ["address"] = operation.Address,
            ["messages"] = new JsonArray(operation.Messages
                .Select(message => (JsonNode?)new JsonObject { ["name"] = message.Name, ["ref"] = message.Pointer })
                .ToArray()),
        };
        if ((String(operation.Node["summary"]) ?? String(operation.Node["description"])) is { } text)
        {
            summary["summary"] = text;
        }

        return summary;
    }

    private static List<AsyncApiOperation> GetOperations2(JsonObject document)
    {
        var operations = new List<AsyncApiOperation>();
        foreach (var (address, node) in document["channels"] as JsonObject ?? [])
        {
            var channelPointer = JsonPointer.Build("channels", address);
            var (_, channel, _) = JsonPointer.Follow(document, channelPointer);
            if (channel is null)
            {
                continue;
            }

            // 2.x: "subscribe" is what the app sends, "publish" what it receives.
            foreach (var (verb, action) in new[] { ("subscribe", "send"), ("publish", "receive") })
            {
                if (channel[verb] is not JsonObject operation)
                {
                    continue;
                }

                var pointer = JsonPointer.Append(channelPointer, verb);
                operations.Add(new AsyncApiOperation(
                    String(operation["operationId"]) ?? $"{verb} {address}",
                    pointer,
                    action,
                    operation,
                    address,
                    channelPointer,
                    channel,
                    address,
                    Messages2(document, operation["message"], JsonPointer.Append(pointer, "message"), verb)));
            }
        }

        return operations;
    }

    /// <summary>The messages of a 2.x operation's <c>message</c>, including each of a <c>oneOf</c>.</summary>
    private static List<AsyncApiMessageReference> Messages2(JsonObject document, JsonNode? message, string pointer, string verb)
    {
        if (message is JsonObject { } single && single["oneOf"] is JsonArray alternatives)
        {
            return alternatives
                .Select((alternative, index) => Message2(document, alternative, JsonPointer.Append(pointer, "oneOf", index.ToString(System.Globalization.CultureInfo.InvariantCulture)), $"{verb} message {index}"))
                .ToList();
        }

        return message is JsonObject ? [Message2(document, message, pointer, $"{verb} message")] : [];
    }

    private static AsyncApiMessageReference Message2(JsonObject document, JsonNode? message, string pointer, string fallbackName)
    {
        var name = LastSegment(JsonPointer.ReferenceOf(message)) ?? String(message?["name"]) ?? String(message?["messageId"]) ?? fallbackName;
        var (canonical, node, problem) = JsonPointer.Follow(document, pointer);
        return new AsyncApiMessageReference(name, canonical, node, problem);
    }

    private static List<AsyncApiOperation> GetOperations3(JsonObject document)
    {
        var operations = new List<AsyncApiOperation>();
        foreach (var (id, _) in document["operations"] as JsonObject ?? [])
        {
            var (pointer, operation, _) = JsonPointer.Follow(document, JsonPointer.Build("operations", id));
            if (operation is null)
            {
                continue;
            }

            var channelReference = JsonPointer.ReferenceOf(operation["channel"]);
            var (channelPointer, channel, _) = channelReference is not null
                ? JsonPointer.Follow(document, channelReference)
                : (null, null, null);

            var messages = (operation["messages"] as JsonArray ?? [])
                .Select((message, index) =>
                {
                    // 3.x operations refer to their channel's messages; an inline one is tolerated, though invalid.
                    var reference = JsonPointer.ReferenceOf(message);
                    var location = reference ?? JsonPointer.Append(pointer, "messages", index.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    var (canonical, node, problem) = JsonPointer.Follow(document, location);
                    var name = LastSegment(reference) ?? String(message?["name"]) ?? $"message {index}";
                    return new AsyncApiMessageReference(name, canonical, node, problem);
                })
                .ToList();

            operations.Add(new AsyncApiOperation(
                id,
                pointer,
                String(operation["action"]) ?? "send",
                operation,
                LastSegment(channelReference),
                channelPointer,
                channel,
                String(channel?["address"]),
                messages));
        }

        return operations;
    }

    private static (string Name, string Pointer, JsonObject? Node, string? Error) LocateMessage(JsonObject document, string message)
    {
        if (JsonPointer.IsLocal(message))
        {
            var (pointer, node, problem) = JsonPointer.Follow(document, message);
            return (LastSegment(pointer) ?? message, pointer, node, problem is null ? null : $"The reference '{message}' {problem}.");
        }

        var components = document["components"]?["messages"] as JsonObject;
        if (components?.Select(entry => entry.Key).FirstOrDefault(key => string.Equals(key, message, StringComparison.OrdinalIgnoreCase)) is { } componentKey)
        {
            var (pointer, node, problem) = JsonPointer.Follow(document, JsonPointer.Build("components", "messages", componentKey));
            return (componentKey, pointer, node, problem is null ? null : $"The message '{componentKey}' {problem}.");
        }

        var matches = MessageCandidates(document)
            .Where(candidate => string.Equals(candidate.Name, message, StringComparison.OrdinalIgnoreCase) && candidate.Node is not null)
            .DistinctBy(candidate => candidate.Pointer, StringComparer.Ordinal)
            .ToList();

        return matches switch
        {
            [var match] => (match.Name, match.Pointer, match.Node, null),
            [] => (message, message, null,
                $"The document has no message '{message}'. Its messages are: {string.Join(", ", MessageNames(document))}."),
            _ => (message, message, null,
                $"'{message}' names {matches.Count} different messages. Pass one of their pointers instead: " +
                $"{string.Join(", ", matches.Select(match => match.Pointer))}."),
        };
    }

    /// <summary>Every message an operation refers to, and in 3.x every message of a channel, by the name it is known by.</summary>
    private static IEnumerable<AsyncApiMessageReference> MessageCandidates(JsonObject document)
    {
        foreach (var operation in GetOperations(document))
        {
            foreach (var message in operation.Messages)
            {
                yield return message;
            }
        }

        if (IsAsyncApi2(document))
        {
            yield break;
        }

        foreach (var (channelKey, _) in document["channels"] as JsonObject ?? [])
        {
            var (channelPointer, channel, _) = JsonPointer.Follow(document, JsonPointer.Build("channels", channelKey));
            foreach (var (name, _) in channel?["messages"] as JsonObject ?? [])
            {
                var (pointer, node, problem) = JsonPointer.Follow(document, JsonPointer.Append(channelPointer, "messages", name));
                yield return new AsyncApiMessageReference(name, pointer, node, problem);
            }
        }
    }
}
