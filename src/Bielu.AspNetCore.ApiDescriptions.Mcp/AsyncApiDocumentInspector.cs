using System.Text.Json.Nodes;

namespace Bielu.AspNetCore.ApiDescriptions.Mcp;

/// <summary>
/// Answers questions about a serialized AsyncAPI document (2.x or 3.x) in a shape that is short enough for an agent's
/// context: its operations, and one message with every schema it references resolved.
/// </summary>
internal static class AsyncApiDocumentInspector
{
    private const string ComponentsPrefix = "#/components/";

    /// <summary>
    /// Summarizes the operations: their id, whether the app sends or receives, the channel and its address, the message
    /// keys, and the summary.
    /// </summary>
    public static JsonArray ListOperations(JsonObject document)
    {
        var operations = new JsonArray();
        var channels = document["channels"] as JsonObject;

        if (IsAsyncApi2(document))
        {
            // 2.x: "subscribe" is what the app sends, "publish" what it receives.
            foreach (var (address, channel) in channels ?? [])
            {
                foreach (var (verb, action) in new[] { ("subscribe", "send"), ("publish", "receive") })
                {
                    if (channel?[verb] is JsonObject operation)
                    {
                        operations.Add(Operation(String(operation["operationId"]) ?? $"{verb} {address}", action,
                            address, address, MessageKeys(operation["message"]), operation));
                    }
                }
            }

            return operations;
        }

        foreach (var (id, node) in document["operations"] as JsonObject ?? [])
        {
            if (node is not JsonObject operation)
            {
                continue;
            }

            var channelKey = LastSegment(String(operation["channel"]?["$ref"]));
            var address = channelKey is not null ? String(channels?[channelKey]?["address"]) : null;
            var messages = (operation["messages"] as JsonArray ?? [])
                .Select(message => LastSegment(String(message?["$ref"])))
                .OfType<string>();
            operations.Add(Operation(id, String(operation["action"]) ?? "send", channelKey, address, messages, operation));
        }

        return operations;
    }

    /// <summary>
    /// Returns the message under <c>components/messages</c> with every schema it references copied in under
    /// <c>definitions</c>, or <see langword="null"/> when the document has no such message.
    /// </summary>
    public static JsonObject? GetMessage(JsonObject document, string messageKey)
    {
        var messages = document["components"]?["messages"] as JsonObject;
        var key = messages?.Select(entry => entry.Key)
            .FirstOrDefault(candidate => string.Equals(candidate, messageKey, StringComparison.OrdinalIgnoreCase));
        if (key is null || messages![key] is not JsonObject message)
        {
            return null;
        }

        var result = (JsonObject)message.DeepClone();
        var definitions = new JsonObject();
        Resolve(result, definitions, document["components"] as JsonObject);
        result["key"] = key;
        if (definitions.Count > 0)
        {
            result["definitions"] = definitions;
        }

        return result;
    }

    /// <summary>The keys of the messages under <c>components/messages</c>.</summary>
    public static IEnumerable<string> MessageKeys(JsonObject document) =>
        (document["components"]?["messages"] as JsonObject ?? []).Select(entry => entry.Key);

    /// <summary>
    /// Rewrites the <c>$ref</c>s to <c>#/components/schemas/X</c> (and other components) in <paramref name="node"/> to
    /// <c>#/definitions/X</c>, copying each referenced component, transitively, into <paramref name="definitions"/>.
    /// </summary>
    private static void Resolve(JsonNode? node, JsonObject definitions, JsonObject? components)
    {
        switch (node)
        {
            case JsonObject value:
                if (String(value["$ref"]) is { } reference && reference.StartsWith(ComponentsPrefix, StringComparison.Ordinal)
                    && reference[ComponentsPrefix.Length..].Split('/') is [var section, var key])
                {
                    var name = section == "schemas" ? key : $"{section}.{key}";
                    value["$ref"] = "#/definitions/" + name;
                    if (!definitions.ContainsKey(name) && components?[section]?[key] is { } target)
                    {
                        var copy = target.DeepClone();
                        definitions[name] = copy;
                        Resolve(copy, definitions, components);
                    }
                }

                foreach (var (_, child) in value.ToList())
                {
                    Resolve(child, definitions, components);
                }

                break;
            case JsonArray array:
                foreach (var child in array)
                {
                    Resolve(child, definitions, components);
                }

                break;
        }
    }

    private static JsonObject Operation(string id, string action, string? channel, string? address,
        IEnumerable<string> messages, JsonObject operation)
    {
        var summary = new JsonObject
        {
            ["id"] = id,
            ["action"] = action,
            ["channel"] = channel,
            ["address"] = address,
            ["messages"] = new JsonArray(messages.Select(message => (JsonNode?)message).ToArray()),
        };
        if ((String(operation["summary"]) ?? String(operation["description"])) is { } text)
        {
            summary["summary"] = text;
        }

        return summary;
    }

    /// <summary>The message keys a 2.x operation's <c>message</c> refers to, including those of a <c>oneOf</c>.</summary>
    private static IEnumerable<string> MessageKeys(JsonNode? message) => message switch
    {
        JsonObject { } single when single["oneOf"] is JsonArray alternatives =>
            alternatives.SelectMany(alternative => MessageKeys(alternative)),
        JsonObject { } single when LastSegment(String(single["$ref"])) is { } key => [key],
        JsonObject { } single when String(single["name"]) is { } name => [name],
        _ => [],
    };

    internal static bool IsAsyncApi2(JsonObject document) =>
        String(document["asyncapi"])?.StartsWith("2.", StringComparison.Ordinal) == true;

    private static string? LastSegment(string? reference) =>
        reference is null ? null : reference[(reference.LastIndexOfAny(['/', '#']) + 1)..];

    private static string? String(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) && text.Length > 0 ? text : null;
}
