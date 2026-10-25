using System.Text.Json.Nodes;

namespace Bielu.AspNetCore.ApiDescriptions.Mcp;

/// <summary>
/// Local JSON Pointer references (<c>#/channels/orders~1placed</c>) as RFC 6901 defines them in a URI fragment: segments
/// are percent-decoded, then <c>~1</c> and <c>~0</c> unescaped. Pointers this package writes are in that canonical form,
/// without percent-encoding, so the same location is always written the same way.
/// </summary>
internal static class JsonPointer
{
    /// <summary>The pointer to the document root.</summary>
    public const string Root = "#";

    /// <summary>The upper bound on <c>$ref</c>s followed in one resolution, so a reference cycle cannot loop forever.</summary>
    private const int MaxReferenceHops = 32;

    /// <summary>Builds a pointer from unescaped segments, for example <c>("channels", "a/b")</c> → <c>#/channels/a~1b</c>.</summary>
    public static string Build(params IEnumerable<string> segments) =>
        Root + string.Concat(segments.Select(segment => "/" + Escape(segment)));

    /// <summary>Appends unescaped segments to a pointer.</summary>
    public static string Append(string pointer, params IEnumerable<string> segments) =>
        pointer + string.Concat(segments.Select(segment => "/" + Escape(segment)));

    public static string Escape(string segment) => segment.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);

    /// <summary>Whether <paramref name="reference"/> points into this document rather than another one.</summary>
    public static bool IsLocal(string reference) => reference.StartsWith(Root, StringComparison.Ordinal);

    /// <summary>
    /// Splits a local reference into its unescaped segments, or returns <see langword="false"/> when it is not a local
    /// JSON Pointer (another document, or a plain-name fragment such as <c>#foo</c>).
    /// </summary>
    public static bool TryParse(string reference, out IReadOnlyList<string> segments)
    {
        segments = [];
        if (!IsLocal(reference))
        {
            return false;
        }

        var fragment = reference[1..];
        if (fragment.Length == 0)
        {
            return true;
        }

        if (fragment[0] != '/')
        {
            return false;
        }

        // An invalid percent-escape is kept as written, as Uri.UnescapeDataString does.
        segments = fragment[1..].Split('/')
            .Select(segment => Uri.UnescapeDataString(segment).Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal))
            .ToArray();
        return true;
    }

    /// <summary>Rewrites a local reference in canonical form, or returns it unchanged when it is not one.</summary>
    public static string Normalize(string reference) => TryParse(reference, out var segments) ? Build(segments) : reference;

    /// <summary>
    /// Finds the node a local reference points to. A <c>$ref</c> met on the way is followed, so
    /// <c>#/channels/orders/messages/created</c> resolves even when the channel itself is a reference.
    /// </summary>
    /// <returns>The node, or <see langword="null"/> when nothing is there or the reference is not local.</returns>
    public static JsonNode? Resolve(JsonNode root, string reference) => Resolve(root, reference, hops: 0);

    /// <summary>
    /// Follows <paramref name="pointer"/> and every <c>$ref</c> the node it finds is, until it reaches a node that is not
    /// a reference.
    /// </summary>
    /// <returns>
    /// The canonical pointer and value of the node at the end of the chain, or <see langword="null"/> and the reason when
    /// the chain is broken, leaves the document, or is a cycle.
    /// </returns>
    public static (string Pointer, JsonObject? Node, string? Problem) Follow(JsonNode root, string pointer)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var current = Normalize(pointer);
        while (true)
        {
            if (!IsLocal(current))
            {
                return (current, null, "points to another document, which is not resolved");
            }

            if (!visited.Add(current) || visited.Count > MaxReferenceHops)
            {
                return (current, null, "is part of a reference cycle");
            }

            switch (Resolve(root, current))
            {
                case JsonObject node when ReferenceOf(node) is { } next:
                    current = Normalize(next);
                    break;
                case JsonObject node:
                    return (current, node, null);
                case null:
                    return (current, null, "does not exist in the document");
                default:
                    return (current, null, "is not an object");
            }
        }
    }

    /// <summary>The <c>$ref</c> of a Reference Object, or <see langword="null"/> when the node is not one.</summary>
    public static string? ReferenceOf(JsonNode? node) =>
        node is JsonObject value && value["$ref"] is JsonValue reference && reference.TryGetValue<string>(out var text) && text.Length > 0
            ? text
            : null;

    private static JsonNode? Resolve(JsonNode root, string reference, int hops)
    {
        if (hops > MaxReferenceHops || !TryParse(reference, out var segments))
        {
            return null;
        }

        var node = root;
        for (var index = 0; index < segments.Count; index++)
        {
            // A reference on the way stands for the node it points to.
            if (ReferenceOf(node) is { } next && IsLocal(next))
            {
                node = Resolve(root, next, hops + 1);
            }

            node = (node, segments[index]) switch
            {
                (JsonObject value, var key) => value[key],
                (JsonArray array, var position) when int.TryParse(position, out var i) && i >= 0 && i < array.Count => array[i],
                _ => null,
            };
            if (node is null)
            {
                return null;
            }
        }

        return node;
    }
}
