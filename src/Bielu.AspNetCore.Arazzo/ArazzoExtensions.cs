using System.Text.Json.Nodes;

namespace Bielu.AspNetCore.Arazzo;

/// <summary>Shared handling of the Specification Extensions (<c>x-*</c>) the builders let callers add.</summary>
internal static class ArazzoExtensions
{
    /// <summary>Sets <paramref name="name"/> to <paramref name="value"/>, after checking it has the required <c>x-</c> prefix.</summary>
    public static void Set(IDictionary<string, JsonNode?> extensions, string name, JsonNode? value)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        if (!name.StartsWith("x-", StringComparison.Ordinal) || name.Length == 2)
        {
            throw new ArgumentException($"'{name}' is not a Specification Extension name; it must start with 'x-'.", nameof(name));
        }

        extensions[name] = value;
    }
}
