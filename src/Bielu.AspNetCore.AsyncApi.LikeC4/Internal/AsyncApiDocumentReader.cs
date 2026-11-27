using System.Text.Json;
using System.Text.Json.Nodes;
using Bielu.Spec.Shared;

namespace Bielu.AspNetCore.AsyncApi.LikeC4.Internal;

/// <summary>
/// Reads the services, channels and operations out of an AsyncAPI 2.x or 3.x document, as JSON or YAML. It reads the
/// document as a tree rather than through an object model, so it takes any document the app serves or a tool wrote,
/// with its local <c>$ref</c>s followed.
/// </summary>
internal static class AsyncApiDocumentReader
{
    private const int MaxReferenceHops = 32;

    /// <summary>Parses <paramref name="content"/>, JSON or YAML, and reads it.</summary>
    /// <exception cref="FormatException">The content is not an AsyncAPI document.</exception>
    public static AsyncApiServiceDescription Read(string content)
    {
        JsonNode? node;
        try
        {
            node = content.TrimStart().StartsWith('{')
                ? JsonNode.Parse(content)
                : YamlToJsonNodeConverter.Convert(new StringReader(content));
        }
        catch (Exception ex) when (ex is JsonException or YamlDotNet.Core.YamlException)
        {
            throw new FormatException($"The document is neither JSON nor YAML: {ex.Message}", ex);
        }

        if (node is not JsonObject root || Text(root["asyncapi"]) is not { } version)
        {
            throw new FormatException("The document is not an AsyncAPI document: it has no 'asyncapi' field.");
        }

        var info = root["info"] as JsonObject;
        var servers = ReadServerProtocols(root);
        var (channels, operations) = IsVersion2(version)
            ? ReadVersion2(root, servers)
            : ReadVersion3(root, servers);
        return new AsyncApiServiceDescription(
            Text(info?["title"]),
            Text(info?["version"]),
            Text(info?["description"]),
            channels,
            operations);
    }

    /// <summary>Maps every server's name to its protocol.</summary>
    private static Dictionary<string, string> ReadServerProtocols(JsonObject root)
    {
        var servers = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, node) in root["servers"] as JsonObject ?? [])
        {
            if (Text(Resolve(root, node)?["protocol"]) is { } protocol)
            {
                servers[name] = protocol;
            }
        }

        return servers;
    }

    private static (List<AsyncApiChannelDescription>, List<AsyncApiOperationDescription>) ReadVersion3(
        JsonObject root, Dictionary<string, string> servers)
    {
        var channels = new List<AsyncApiChannelDescription>();
        foreach (var (key, node) in root["channels"] as JsonObject ?? [])
        {
            if (Resolve(root, node) is not JsonObject channel)
            {
                continue;
            }

            var serverNames = (channel["servers"] as JsonArray ?? [])
                .Select(server => LastToken(Text(server?["$ref"])))
                .OfType<string>();
            var messages = (channel["messages"] as JsonObject ?? [])
                .Select(message => MessageName(root, message.Key, message.Value));
            channels.Add(new AsyncApiChannelDescription(
                key,
                // A 3.x channel without an address, or with a null or empty one, has an unknown address. The address
                // is the channel's identity, so it is read as written, whitespace and all.
                channel["address"] is JsonValue address && address.TryGetValue<string>(out var text) && text.Length > 0 ? text : null,
                Text(channel["title"]),
                Text(channel["description"]),
                Protocols(servers, serverNames),
                Distinct(messages)));
        }

        var operations = new List<AsyncApiOperationDescription>();
        foreach (var (key, node) in root["operations"] as JsonObject ?? [])
        {
            if (Resolve(root, node) is not JsonObject operation
                || ReadAction(Text(operation["action"])) is not { } action
                || ChannelKey(Text(operation["channel"]?["$ref"])) is not { } channelKey)
            {
                continue;
            }

            var channelMessages = Resolve(root, root["channels"]?[channelKey])?["messages"] as JsonObject;
            var messages = (operation["messages"] as JsonArray ?? [])
                .Select(reference => Text(reference?["$ref"]))
                .OfType<string>()
                .Select(reference =>
                {
                    var messageKey = LastToken(reference) ?? reference;
                    return MessageName(root, messageKey, Resolve(root, reference));
                })
                .ToList();
            operations.Add(new AsyncApiOperationDescription(
                key,
                action,
                channelKey,
                Text(operation["summary"]) ?? Text(operation["title"]),
                messages.Count > 0
                    ? Distinct(messages)
                    : Distinct((channelMessages ?? []).Select(message => MessageName(root, message.Key, message.Value)))));
        }

        return (channels, operations);
    }

    /// <summary>
    /// Reads a 2.x document. Its operations are named from the client's side: a channel's <c>publish</c> operation
    /// is what clients publish, so the application receives it, and <c>subscribe</c> is what the application sends.
    /// </summary>
    private static (List<AsyncApiChannelDescription>, List<AsyncApiOperationDescription>) ReadVersion2(
        JsonObject root, Dictionary<string, string> servers)
    {
        var channels = new List<AsyncApiChannelDescription>();
        var operations = new List<AsyncApiOperationDescription>();
        foreach (var (address, node) in root["channels"] as JsonObject ?? [])
        {
            if (Resolve(root, node) is not JsonObject channel)
            {
                continue;
            }

            var serverNames = (channel["servers"] as JsonArray ?? []).Select(Text).OfType<string>();
            var channelMessages = new List<string>();
            foreach (var (field, action) in new[] { ("publish", AsyncApiOperationAction.Receive), ("subscribe", AsyncApiOperationAction.Send) })
            {
                if (Resolve(root, channel[field]) is not JsonObject operation)
                {
                    continue;
                }

                var messages = Version2Messages(root, operation["message"]);
                channelMessages.AddRange(messages);
                operations.Add(new AsyncApiOperationDescription(
                    Text(operation["operationId"]) ?? $"{address}/{field}",
                    action,
                    address,
                    Text(operation["summary"]),
                    messages));
            }

            channels.Add(new AsyncApiChannelDescription(
                address,
                address,
                Text(channel["title"]),
                Text(channel["description"]),
                Protocols(servers, serverNames),
                Distinct(channelMessages)));
        }

        return (channels, operations);
    }

    /// <summary>The messages of a 2.x operation: a single message, or the branches of its <c>oneOf</c>.</summary>
    private static IReadOnlyList<string> Version2Messages(JsonObject root, JsonNode? message)
    {
        var resolved = Resolve(root, message);
        var branches = resolved?["oneOf"] is JsonArray oneOf ? oneOf.ToList() : message is null ? [] : [message];
        return Distinct(branches.Select(branch => MessageName(root, null, branch)));
    }

    /// <summary>
    /// A message's name: its <c>name</c>, else its <c>title</c>, else the key it is referenced by or listed under.
    /// The name and title may come from its traits.
    /// </summary>
    private static string MessageName(JsonObject root, string? key, JsonNode? node)
    {
        var resolved = Resolve(root, node);
        return MessageField(root, resolved, "name")
               ?? MessageField(root, resolved, "title")
               ?? LastToken(Text(node?["$ref"]))
               ?? key
               ?? "message";
    }

    /// <summary>
    /// A message's <paramref name="field"/> after its traits are applied, in order. A 2.x trait is merged over the
    /// message, so the last trait that sets the field wins; a 3.x message keeps its own value over any trait's.
    /// </summary>
    private static string? MessageField(JsonObject root, JsonNode? message, string field)
    {
        var own = Text(message?[field]);
        if (own is not null && !IsVersion2(Text(root["asyncapi"])))
        {
            return own;
        }

        var fromTraits = (message?["traits"] as JsonArray ?? [])
            .Select(trait => Text(Resolve(root, trait)?[field]))
            .LastOrDefault(value => value is not null);
        return fromTraits ?? own;
    }

    private static bool IsVersion2(string? version) => version?.StartsWith("2.", StringComparison.Ordinal) == true;

    /// <summary>The distinct, sorted protocols of the named servers, or of every server when none is named.</summary>
    private static IReadOnlyList<string> Protocols(Dictionary<string, string> servers, IEnumerable<string> names)
    {
        var named = names.ToList();
        var protocols = named.Count > 0
            ? named.Select(name => servers.GetValueOrDefault(name)).OfType<string>()
            : servers.Values;
        return protocols.Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal).ToList();
    }

    private static AsyncApiOperationAction? ReadAction(string? action) => action switch
    {
        "send" => AsyncApiOperationAction.Send,
        "receive" => AsyncApiOperationAction.Receive,
        _ => null,
    };

    /// <summary>The key of the channel a <c>#/channels/&lt;key&gt;</c> reference points at.</summary>
    private static string? ChannelKey(string? reference)
    {
        const string prefix = "#/channels/";
        reference = Decode(reference);
        if (reference is null || !reference.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var rest = reference[prefix.Length..];
        return rest.Contains('/') ? null : Unescape(rest);
    }

    /// <summary>
    /// Follows <paramref name="node"/>'s local <c>$ref</c>, and the one that points at, until it reaches an object
    /// that is not a reference. A reference to another document, or one that leads nowhere, gives <see langword="null"/>.
    /// </summary>
    internal static JsonNode? Resolve(JsonObject root, JsonNode? node)
    {
        for (var hop = 0; hop < MaxReferenceHops; hop++)
        {
            if (Text(node?["$ref"]) is not { } reference)
            {
                return node;
            }

            node = Resolve(root, reference);
        }

        return null;
    }

    /// <summary>Looks up a local <c>#/…</c> JSON Pointer in <paramref name="root"/>.</summary>
    private static JsonNode? Resolve(JsonObject root, string reference)
    {
        reference = Uri.UnescapeDataString(reference);
        if (!reference.StartsWith("#/", StringComparison.Ordinal))
        {
            return null;
        }

        JsonNode? current = root;
        foreach (var token in reference[2..].Split('/'))
        {
            current = current is JsonObject parent ? parent[Unescape(token)] : null;
            if (current is null)
            {
                return null;
            }
        }

        return current;
    }

    private static string? LastToken(string? reference) =>
        Decode(reference) is not { } decoded ? null : Unescape(decoded[(decoded.LastIndexOf('/') + 1)..]) is { Length: > 0 } token ? token : null;

    /// <summary>
    /// Percent-decodes a reference: a JSON Pointer in a URI fragment is decoded before it is split into tokens, so
    /// <c>#/channels/order%20events</c> points at the <c>order events</c> channel.
    /// </summary>
    private static string? Decode(string? reference) => reference is null ? null : Uri.UnescapeDataString(reference);

    private static string Unescape(string token) => token.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);

    private static IReadOnlyList<string> Distinct(IEnumerable<string> values) => values.Distinct(StringComparer.Ordinal).ToList();

    internal static string? Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text) ? text.Trim() : null;
}
