using System.Text;

namespace Bielu.AspNetCore.ApiDescriptions.Mcp.Tools;

/// <summary>
/// The opaque continuation token of a paged tool result: which kind of page it continues, a value it is bound to (such
/// as a document fingerprint), and where the next page starts.
/// </summary>
internal static class Cursor
{
    public static string Create(string purpose, string scope, int offset) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"{purpose}|{scope}|{offset}"));

    /// <summary>
    /// Reads a cursor <see cref="Create"/> made for <paramref name="purpose"/>, or returns <see langword="false"/> when it
    /// is malformed or was made for another purpose.
    /// </summary>
    public static bool TryRead(string cursor, string purpose, out string scope, out int offset)
    {
        scope = string.Empty;
        offset = 0;
        string text;
        try
        {
            text = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
        }
        catch (FormatException)
        {
            return false;
        }

        // The scope may itself contain '|', so the purpose and offset are taken from the ends.
        var first = text.IndexOf('|', StringComparison.Ordinal);
        var last = text.LastIndexOf('|');
        if (first < 0 || last <= first || text[..first] != purpose || !int.TryParse(text[(last + 1)..], out offset) ||
            offset < 0)
        {
            return false;
        }

        scope = text[(first + 1)..last];
        return true;
    }
}
