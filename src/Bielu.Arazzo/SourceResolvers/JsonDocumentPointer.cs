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
    public static JsonObject? EvaluateObject(JsonNode? root, string pointer)
    {
        // Uri.UnescapeDataString leaves a malformed escape as it is rather than throwing.
        var decoded = Uri.UnescapeDataString(pointer);
        if (root is null || decoded.Length == 0 || decoded[0] != '/')
        {
            return null;
        }

        var current = root;
        foreach (var rawToken in decoded[1..].Split('/'))
        {
            if (!TryUnescape(rawToken, out var token))
            {
                return null;
            }

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
