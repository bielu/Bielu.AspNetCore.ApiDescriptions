using System.Text;
using System.Text.Json.Nodes;

namespace Bielu.AspNetCore.ApiDescriptions.Mcp;

/// <summary>
/// Plain text matching of AsyncAPI operations: the query and each searchable field are split into lower-case words
/// (identifiers at their camel-case and digit boundaries, a plural <c>s</c> dropped), and a query word matches a field
/// word it equals or begins. Operations matching more query words rank first, then those matching in weightier fields
/// (the id and names over descriptions).
/// </summary>
internal static class AsyncApiOperationSearch
{
    private const int IdWeight = 5;
    private const int NameWeight = 3;
    private const int TextWeight = 2;
    private const int DescriptionWeight = 1;

    /// <summary>The words of a query, or none for an empty one, which then matches everything.</summary>
    public static IReadOnlyList<string> Terms(string? query) => Words(query).Distinct(StringComparer.Ordinal).ToList();

    /// <summary>How many of <paramref name="terms"/> the operation matches, and how strongly.</summary>
    public static (int Matched, int Score) Match(JsonObject document, AsyncApiOperation operation, IReadOnlyList<string> terms)
    {
        if (terms.Count == 0)
        {
            return (0, 0);
        }

        var fields = Fields(document, operation)
            .Select(field => (Words: Words(field.Text).ToHashSet(StringComparer.Ordinal), field.Weight))
            .ToList();

        var matched = 0;
        var score = 0;
        foreach (var term in terms)
        {
            var weight = fields
                .Where(field => field.Words.Any(word => word == term || (term.Length >= 3 && word.StartsWith(term, StringComparison.Ordinal))))
                .Select(field => field.Weight)
                .DefaultIfEmpty(0)
                .Max();
            if (weight > 0)
            {
                matched++;
                score += weight;
            }
        }

        return (matched, score);
    }

    /// <summary>
    /// The protocols an operation is reachable over: those of its servers, and those it or its channel has bindings for.
    /// </summary>
    public static IReadOnlyList<string> Protocols(JsonObject document, AsyncApiOperation operation) =>
        AsyncApiDocumentInspector.GetServers(document, operation, out _)
            .Select(server => AsyncApiDocumentInspector.String(server.Node?["protocol"]))
            .Concat(BindingKeys(document, operation.Node["bindings"]))
            .Concat(BindingKeys(document, operation.Channel?["bindings"]))
            .OfType<string>()
            .Select(protocol => protocol.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();

    private static IEnumerable<(string? Text, int Weight)> Fields(JsonObject document, AsyncApiOperation operation)
    {
        var node = operation.Node;
        yield return (operation.Id, IdWeight);
        yield return (AsyncApiDocumentInspector.String(node["title"]), NameWeight);
        yield return (AsyncApiDocumentInspector.String(node["summary"]), TextWeight);
        yield return (AsyncApiDocumentInspector.String(node["description"]), DescriptionWeight);
        foreach (var tag in node["tags"] as JsonArray ?? [])
        {
            yield return (AsyncApiDocumentInspector.String(Resolve(document, tag)?["name"]), NameWeight);
        }

        yield return (operation.ChannelKey, NameWeight);
        yield return (operation.Address, NameWeight);
        yield return (AsyncApiDocumentInspector.String(operation.Channel?["description"]), DescriptionWeight);
        foreach (var message in operation.Messages)
        {
            yield return (message.Name, NameWeight);
            yield return (AsyncApiDocumentInspector.String(message.Node?["name"]), NameWeight);
            yield return (AsyncApiDocumentInspector.String(message.Node?["title"]), TextWeight);
            yield return (AsyncApiDocumentInspector.String(message.Node?["summary"]), TextWeight);
        }

        // Binding values name the protocol-level target, for example a SignalR hub method or a Kafka topic.
        foreach (var binding in Bindings(document, node["bindings"]))
        {
            foreach (var (_, value) in binding)
            {
                yield return (AsyncApiDocumentInspector.String(value), TextWeight);
            }
        }
    }

    private static IEnumerable<JsonObject> Bindings(JsonObject document, JsonNode? bindings) =>
        (Resolve(document, bindings) ?? []).Select(entry => Resolve(document, entry.Value)).OfType<JsonObject>();

    private static IEnumerable<string> BindingKeys(JsonObject document, JsonNode? bindings) =>
        (Resolve(document, bindings) ?? []).Select(entry => entry.Key);

    /// <summary>The object itself, or the one it refers to when it is a Reference Object.</summary>
    private static JsonObject? Resolve(JsonObject document, JsonNode? node) =>
        JsonPointer.ReferenceOf(node) is { } reference ? JsonPointer.Follow(document, reference).Node : node as JsonObject;

    /// <summary>Splits text into lower-case words, breaking identifiers such as <c>sendChatMessage</c> or <c>v2Orders</c>.</summary>
    private static IEnumerable<string> Words(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            yield break;
        }

        var word = new StringBuilder();
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (!char.IsLetterOrDigit(character))
            {
                if (Flush(word) is { } completed)
                {
                    yield return completed;
                }

                continue;
            }

            if (word.Length > 0 && IsBoundary(text, index))
            {
                if (Flush(word) is { } completed)
                {
                    yield return completed;
                }
            }

            word.Append(char.ToLowerInvariant(character));
        }

        if (Flush(word) is { } last)
        {
            yield return last;
        }
    }

    /// <summary>
    /// Whether a word starts at <paramref name="index"/>: lower to upper case (<c>chatMessage</c>), the last capital of
    /// an acronym (<c>HTTPServer</c>), or a change between letters and digits (<c>v2</c>).
    /// </summary>
    private static bool IsBoundary(string text, int index)
    {
        var previous = text[index - 1];
        var current = text[index];
        return (char.IsLower(previous) && char.IsUpper(current))
               || (char.IsUpper(previous) && char.IsUpper(current) && index + 1 < text.Length && char.IsLower(text[index + 1]))
               || (char.IsDigit(previous) != char.IsDigit(current) && char.IsLetterOrDigit(previous));
    }

    private static string? Flush(StringBuilder word)
    {
        if (word.Length == 0)
        {
            return null;
        }

        var text = word.ToString();
        word.Clear();

        // "messages" finds "message"; "address" and "status" keep their s.
        return text.Length > 3 && text[^1] == 's' && text[^2] is not ('s' or 'u') ? text[..^1] : text;
    }
}
