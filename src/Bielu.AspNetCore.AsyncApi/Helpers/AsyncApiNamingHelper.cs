using System.Text.RegularExpressions;

namespace Bielu.AspNetCore.AsyncApi.Helpers;

internal static class AsyncApiNamingHelper
{
    public static string SanitizeKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return key;
        return Regex.Replace(key, @"[^a-zA-Z0-9\.\-_]", string.Empty);
    }
}
