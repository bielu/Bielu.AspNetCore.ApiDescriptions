using System.Text.Json.Nodes;

namespace Bielu.Arazzo.SourceResolvers;

/// <summary>
/// Evaluates the JSON Pointer from an Arazzo reference's URI fragment (the part after <c>#</c>) against a
/// <see cref="JsonNode"/> document, without copying it. Percent-escapes are decoded first, since the pointer
/// arrives inside a URI fragment (RFC 6901 §6), then each token's <c>~1</c>/<c>~0</c> escapes.
/// </summary>
internal static class JsonDocumentPointer
{
    /// <summary>Returns the object the pointer addresses, or <see langword="null"/> when it addresses nothing or a non-object.</summary>
    public static JsonObject? EvaluateObject(JsonNode? root, string pointer) =>
        root is not null && Tokens(pointer) is { } tokens ? EvaluateObject(root, tokens) : null;

    /// <summary>
    /// The pointer's reference tokens, decoded, or <see langword="null"/> when it is not a pointer (empty, not
    /// starting with <c>/</c>, or with a malformed <c>~</c> escape).
    /// </summary>
    public static string[]? Tokens(string pointer)
    {
        // Uri.UnescapeDataString leaves a malformed escape as it is rather than throwing.
        var decoded = Uri.UnescapeDataString(pointer);
        if (decoded.Length == 0 || decoded[0] != '/')
        {
            return null;
        }

        var raw = decoded[1..].Split('/');
        var tokens = new string[raw.Length];
        for (var i = 0; i < raw.Length; i++)
        {
            if (!TryUnescape(raw[i], out tokens[i]))
            {
                return null;
            }
        }

        return tokens;
    }

    /// <summary>Returns the object <paramref name="tokens"/> address, or <see langword="null"/> when they address nothing or a non-object.</summary>
    public static JsonObject? EvaluateObject(JsonNode root, IEnumerable<string> tokens)
    {
        JsonNode? current = root;
        foreach (var token in tokens)
        {
            current = current switch
            {
                JsonObject obj when obj.TryGetPropertyValue(token, out var child) => child,
                JsonArray array when int.TryParse(token, System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out var index) && index < array.Count => array[index],
                _ => null,
            };

            if (current is null)
            {
                return null;
            }
        }

        return current as JsonObject;
    }

    /// <summary>
    /// <paramref name="node"/> with its local <c>$ref</c>s (<c>#/…</c>) followed to the object they end at, or
    /// <see langword="null"/> when one points at nothing, outside the document, or back into the chain.
    /// </summary>
    public static JsonObject? FollowLocalReferences(JsonNode root, JsonObject node)
    {
        var seen = new HashSet<JsonObject>(ReferenceEqualityComparer.Instance);
        var current = node;
        while (current["$ref"] is JsonValue reference)
        {
            if (!seen.Add(current)
                || !reference.TryGetValue<string>(out var target)
                || !target.StartsWith("#/", StringComparison.Ordinal)
                || EvaluateObject(root, target[1..]) is not { } next)
            {
                return null;
            }

            current = next;
        }

        return current;
    }

    private static bool TryUnescape(string rawToken, out string token)
    {
        token = string.Empty;
        var builder = new System.Text.StringBuilder(rawToken.Length);
        for (var i = 0; i < rawToken.Length; i++)
        {
            if (rawToken[i] != '~')
            {
                builder.Append(rawToken[i]);
                continue;
            }

            if (i + 1 >= rawToken.Length || (rawToken[i + 1] != '0' && rawToken[i + 1] != '1'))
            {
                return false;
            }

            builder.Append(rawToken[i + 1] == '0' ? '~' : '/');
            i++;
        }

        token = builder.ToString();
        return true;
    }
}
