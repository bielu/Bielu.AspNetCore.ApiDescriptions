namespace Bielu.AspNetCore.Arazzo.SourceResolvers;

/// <summary>
/// Strictly unescapes a single RFC 6901 JSON Pointer reference-token. Shared by <see cref="AsyncApiSourceResolver"/>
/// and <see cref="OpenApiSourceResolver"/>, whose pointer shapes (<c>/channels/{name}</c>,
/// <c>/operations/{id}</c>, <c>/paths/{path}/{method}</c>) are each a fixed prefix followed by exactly one
/// escaped segment — never a raw, unescaped <c>/</c>, which per the spec always means "one level deeper" and
/// so can't validly appear inside a single segment.
/// </summary>
internal static class JsonPointerSegment
{
    /// <summary>
    /// Unescapes <paramref name="segment"/>, rejecting a raw (unescaped) <c>/</c> — which would mean this
    /// isn't actually a single segment — and a <c>~</c> not followed by <c>0</c> or <c>1</c>, per RFC 6901.
    /// </summary>
    public static bool TryUnescape(string segment, out string unescaped)
    {
        unescaped = string.Empty;
        if (segment.Contains('/'))
        {
            return false;
        }

        var builder = new System.Text.StringBuilder(segment.Length);
        for (var i = 0; i < segment.Length; i++)
        {
            if (segment[i] != '~')
            {
                builder.Append(segment[i]);
                continue;
            }

            if (i + 1 >= segment.Length || (segment[i + 1] != '0' && segment[i + 1] != '1'))
            {
                return false;
            }

            builder.Append(segment[i + 1] == '0' ? '~' : '/');
            i++;
        }

        unescaped = builder.ToString();
        return true;
    }
}
