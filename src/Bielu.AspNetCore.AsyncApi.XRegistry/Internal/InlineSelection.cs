using Microsoft.Extensions.Primitives;

namespace Bielu.AspNetCore.AsyncApi.XRegistry.Internal;

/// <summary>
/// The <c>?inline</c> flag of a request: which nested collections and large attributes to include, as dot-separated
/// paths relative to the entity the request is directed at.
/// </summary>
internal sealed class InlineSelection
{
    private const string All = "*";
    private static readonly string[] RootConfiguration = ["model", "capabilities"];

    private readonly List<string> _paths;

    private InlineSelection(List<string> paths) => _paths = paths;

    public static InlineSelection None { get; } = new([]);

    /// <summary>
    /// Parses the flag. A flag without a value means <c>*</c>. Returns <see langword="null"/> and the offending value
    /// when a path is not one of <paramref name="validPaths"/> (or one of them followed by <c>.*</c>, or <c>*</c>).
    /// </summary>
    public static InlineSelection? Parse(StringValues values, IReadOnlyCollection<string> validPaths, out string? invalid)
    {
        invalid = null;
        var paths = new List<string>();
        foreach (var value in values)
        {
            foreach (var path in (string.IsNullOrEmpty(value) ? All : value).Split(',',
                         StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                var parent = path.EndsWith(".*", StringComparison.Ordinal) ? path[..^2] : null;
                if (path != All && !validPaths.Contains(path) && (parent is null || !validPaths.Contains(parent)))
                {
                    invalid = path;
                    return null;
                }

                paths.Add(path);
            }
        }

        return new InlineSelection(paths);
    }

    /// <summary>
    /// Whether the attribute at <paramref name="path"/> is inlined: named itself, named as the parent of an inlined path,
    /// or under a <c>*</c>. <c>*</c> at the root does not inline <c>model</c> or <c>capabilities</c>.
    /// </summary>
    public bool Includes(string path) => _paths.Any(selected =>
        selected == path
        || selected.StartsWith(path + ".", StringComparison.Ordinal)
        || selected == All && !RootConfiguration.Contains(path)
        || selected.EndsWith(".*", StringComparison.Ordinal)
           && (path == selected[..^2] || path.StartsWith(selected[..^1], StringComparison.Ordinal)));
}
