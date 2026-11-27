using Bielu.Cli.Shared;

namespace Bielu.AspNetCore.AsyncApi.Cli.Commands;

/// <summary>
/// Reads the documents the likec4 commands take, each given as <c>[name=]&lt;path|url&gt;</c>: from a file, or over
/// HTTP for a URL.
/// </summary>
internal sealed class LikeC4DocumentLoader : IDisposable
{
    private static readonly string[] DocumentSuffixes = [".json", ".yaml", ".yml", ".asyncapi", ".arazzo"];

    private readonly HttpClient _http;
    private readonly ICliLogger _logger;

    /// <param name="logger">Where a document that cannot be read is reported.</param>
    /// <param name="httpHandler">The handler URLs are fetched through; the default one when <see langword="null"/>.</param>
    public LikeC4DocumentLoader(ICliLogger logger, HttpMessageHandler? httpHandler)
    {
        _logger = logger;
        _http = httpHandler is null ? new HttpClient() : new HttpClient(httpHandler, disposeHandler: false);
        _http.Timeout = TimeSpan.FromSeconds(30);
    }

    public void Dispose() => _http.Dispose();

    /// <summary>
    /// The documents to read, as <c>(name, path or URL)</c>: a named source as given, an unnamed one named after its
    /// file, and an unnamed glob expanded into every file it matches.
    /// </summary>
    public static IEnumerable<(string Name, string Location)> Expand(IEnumerable<string> sources)
    {
        foreach (var source in sources)
        {
            var separator = source.IndexOf('=', StringComparison.Ordinal);
            if (separator > 0 && !IsUrl(source[..separator]))
            {
                yield return (source[..separator], source[(separator + 1)..]);
                continue;
            }

            var locations = IsUrl(source) ? [source] : CliFileResolver.ExpandFilePatterns([source]);
            foreach (var location in locations)
            {
                yield return (NameOf(location), location);
            }
        }
    }

    /// <summary><c>obj/orders.asyncapi.json</c> → <c>orders</c>; a URL is named after its last segment the same way.</summary>
    public static string NameOf(string location)
    {
        var name = IsUrl(location) ? new Uri(location).Segments[^1].TrimEnd('/') : Path.GetFileName(location);
        for (var trimmed = true; trimmed;)
        {
            trimmed = false;
            foreach (var suffix in DocumentSuffixes)
            {
                if (name.Length > suffix.Length && name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    name = name[..^suffix.Length];
                    trimmed = true;
                }
            }
        }

        return name.Length > 0 ? name : location;
    }

    /// <summary>A URL links to itself; a file, by its path relative to the model, which LikeC4 resolves from there.</summary>
    public static string DefaultLink(string location, string outputDirectory) =>
        IsUrl(location) ? location : Path.GetRelativePath(outputDirectory, Path.GetFullPath(location)).Replace('\\', '/');

    /// <summary>Reads a document, reporting it when it cannot be read.</summary>
    public bool TryRead(string location, out string content)
    {
        try
        {
            content = IsUrl(location)
                ? _http.GetStringAsync(location).GetAwaiter().GetResult()
                : File.ReadAllText(location);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or HttpRequestException or TaskCanceledException)
        {
            _logger.Error($"Could not read '{location}': {ex.Message}");
            content = string.Empty;
            return false;
        }
    }

    /// <summary>Reads every source of <paramref name="sources"/>, or returns <see langword="null"/> at the first that cannot be read.</summary>
    public List<(string Name, string Location, string Content)>? TryReadAll(IEnumerable<string> sources)
    {
        var documents = new List<(string, string, string)>();
        foreach (var (name, location) in Expand(sources))
        {
            if (!TryRead(location, out var content))
            {
                return null;
            }

            documents.Add((name, location, content));
        }

        return documents;
    }

    private static bool IsUrl(string value) =>
        value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
}
