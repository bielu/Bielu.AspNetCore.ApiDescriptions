using System.Text.Json;

namespace Bielu.AspNetCore.Scalar.Plugins;

/// <summary>
/// Passes a document-type plugin's options to its bundle. The Scalar.AspNetCore package writes them to the page as a
/// window global (<see cref="ToGlobalScript" />); the Aspire package, whose only seam is the plugin module's URL, puts
/// them in its query string (<see cref="WithQuery" />). The bundle reads both by the options' JavaScript names.
/// </summary>
internal static class ScalarPluginOptions
{
    /// <summary>
    /// Whether a name is a specification extension: <c>x-</c> followed by at least one character. The npm packages
    /// apply the same rule.
    /// </summary>
    internal static bool IsSpecificationExtension(string value) =>
        value.Length > 2 && value.StartsWith("x-", StringComparison.Ordinal);

    /// <summary>
    /// The set options among <paramref name="options" />, each by its JavaScript name (the property name with its first
    /// letter lower-cased), in the order given. Unset (<see langword="null" />) options are left out.
    /// </summary>
    /// <param name="options">Each option's .NET property name and value, such as <c>(nameof(PageExtension), PageExtension)</c>.</param>
    /// <param name="parameterName">The parameter named by the exception: the caller's <c>configure</c> delegate.</param>
    /// <exception cref="ArgumentException">A value is not a specification extension.</exception>
    internal static IReadOnlyList<KeyValuePair<string, string>> SpecificationExtensions(
        IEnumerable<(string PropertyName, string? Value)> options,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(options);

        var set = new List<KeyValuePair<string, string>>();
        foreach (var (propertyName, value) in options)
        {
            if (value is null)
            {
                continue;
            }

            if (!IsSpecificationExtension(value))
            {
                throw new ArgumentException(
                    $"{propertyName} '{value}' is not a specification extension: it must be 'x-' followed by a name.",
                    parameterName);
            }

            set.Add(new(char.ToLowerInvariant(propertyName[0]) + propertyName[1..], value));
        }

        return set;
    }

    /// <summary>
    /// A script element that sets <c>window.<paramref name="globalName" /></c> to the options, as a JSON object in their
    /// order, or an empty string when there are none.
    /// </summary>
    /// <remarks>
    /// <see cref="JsonSerializer" /> escapes <c>&lt;</c>, <c>&gt;</c> and <c>&amp;</c> by default, so a value cannot close
    /// the script element.
    /// </remarks>
    internal static string ToGlobalScript(string globalName, IReadOnlyList<KeyValuePair<string, string>> options)
    {
        ArgumentException.ThrowIfNullOrEmpty(globalName);
        ArgumentNullException.ThrowIfNull(options);

        if (options.Count == 0)
        {
            return string.Empty;
        }

        var json = JsonSerializer.Serialize(options.ToDictionary(option => option.Key, option => option.Value), ScalarPluginOptionsJsonContext.Default.DictionaryStringString);
        return $"<script>window.{globalName} = {json};</script>";
    }

    /// <summary>
    /// Sets <paramref name="parameters" /> in the query string of <paramref name="url" />, replacing any of the
    /// same name and keeping the others and the fragment. A URL without parameters to set is returned as it is.
    /// </summary>
    internal static string WithQuery(string url, IReadOnlyList<KeyValuePair<string, string>> parameters)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(parameters);

        if (parameters.Count == 0)
        {
            return url;
        }

        var hash = url.IndexOf('#', StringComparison.Ordinal);
        var fragment = hash >= 0 ? url[hash..] : string.Empty;
        var beforeFragment = hash >= 0 ? url[..hash] : url;
        var question = beforeFragment.IndexOf('?', StringComparison.Ordinal);
        var path = question >= 0 ? beforeFragment[..question] : beforeFragment;
        var query = question >= 0 ? beforeFragment[(question + 1)..] : string.Empty;

        var names = parameters.Select(parameter => parameter.Key).ToHashSet(StringComparer.Ordinal);
        var kept = query
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(pair => !names.Contains(Uri.UnescapeDataString(pair.Split('=', 2)[0])));
        var added = parameters.Select(parameter => $"{Uri.EscapeDataString(parameter.Key)}={Uri.EscapeDataString(parameter.Value)}");

        return $"{path}?{string.Join('&', kept.Concat(added))}{fragment}";
    }
}
