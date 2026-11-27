using System.Text;
using Bielu.AspNetCore.AsyncApi.LikeC4;
using Bielu.Cli.Shared;

namespace Bielu.AspNetCore.AsyncApi.Cli.Commands;

/// <summary>
/// Worker that writes the LikeC4 model of AsyncAPI documents (see <see cref="AsyncApiLikeC4Generator"/>).
/// </summary>
internal sealed class LikeC4CommandWorker
{
    private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private static readonly string[] DocumentSuffixes = [".json", ".yaml", ".yml", ".asyncapi"];

    private readonly LikeC4CommandContext _context;
    private readonly ICliLogger _logger;
    private readonly HttpMessageHandler? _httpHandler;

    /// <param name="context">The parsed arguments.</param>
    /// <param name="writeInfo">Writes a line of progress.</param>
    /// <param name="writeError">Writes an error.</param>
    /// <param name="httpHandler">The handler documents given by URL are fetched through; the default one when <see langword="null"/>.</param>
    public LikeC4CommandWorker(
        LikeC4CommandContext context,
        Action<string> writeInfo,
        Action<string> writeError,
        HttpMessageHandler? httpHandler = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(writeInfo);
        ArgumentNullException.ThrowIfNull(writeError);

        _context = context;
        _logger = new DelegatingCliLogger(writeInfo, writeInfo, writeError);
        _httpHandler = httpHandler;
    }

    public int Process()
    {
        if (!TryReadLinks(out var links))
        {
            return CliExitCode.Failure;
        }

        var outputDirectory = Path.GetDirectoryName(Path.GetFullPath(_context.OutputPath))!;
        using var http = _httpHandler is null ? new HttpClient() : new HttpClient(_httpHandler, disposeHandler: false);
        http.Timeout = TimeSpan.FromSeconds(30);

        var sources = new List<AsyncApiLikeC4Source>();
        foreach (var (name, location) in ExpandSources())
        {
            if (!TryRead(http, location, out var content))
            {
                return CliExitCode.Failure;
            }

            sources.Add(new AsyncApiLikeC4Source(name, content)
            {
                Link = links.GetValueOrDefault(name) ?? DefaultLink(location, outputDirectory),
            });
        }

        if (sources.Count == 0)
        {
            _logger.Error("No AsyncAPI documents found.");
            return CliExitCode.Failure;
        }

        string model;
        try
        {
            model = AsyncApiLikeC4Generator.Generate(sources);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            _logger.Error(ex.Message);
            return CliExitCode.Failure;
        }

        Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(_context.OutputPath, model, Utf8WithoutBom);
        _logger.Info($"LikeC4 model of {sources.Count} AsyncAPI document(s) written to '{_context.OutputPath}'.");
        return CliExitCode.Success;
    }

    /// <summary>
    /// The documents to read, as <c>(name, path or URL)</c>: a named source as given, an unnamed one named after its
    /// file, and an unnamed glob expanded into every file it matches.
    /// </summary>
    private IEnumerable<(string Name, string Location)> ExpandSources()
    {
        foreach (var source in _context.Sources)
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
    internal static string NameOf(string location)
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
    internal static string DefaultLink(string location, string outputDirectory) =>
        IsUrl(location) ? location : Path.GetRelativePath(outputDirectory, Path.GetFullPath(location)).Replace('\\', '/');

    private bool TryReadLinks(out Dictionary<string, string> links)
    {
        links = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var link in _context.Links)
        {
            var separator = link.IndexOf('=', StringComparison.Ordinal);
            if (separator <= 0 || separator == link.Length - 1)
            {
                _logger.Error($"--link must be name=<url>: '{link}'.");
                return false;
            }

            links[link[..separator]] = link[(separator + 1)..];
        }

        return true;
    }

    private bool TryRead(HttpClient http, string location, out string content)
    {
        try
        {
            content = IsUrl(location)
                ? http.GetStringAsync(location).GetAwaiter().GetResult()
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

    private static bool IsUrl(string value) =>
        value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
}
