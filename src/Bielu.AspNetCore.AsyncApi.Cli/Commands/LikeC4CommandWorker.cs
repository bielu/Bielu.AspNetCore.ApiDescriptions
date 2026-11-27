using System.Text;
using Bielu.AspNetCore.AsyncApi.LikeC4;
using Bielu.Cli.Shared;

namespace Bielu.AspNetCore.AsyncApi.Cli.Commands;

/// <summary>
/// Worker that writes the LikeC4 model of AsyncAPI documents and the dynamic views of Arazzo workflows (see
/// <see cref="AsyncApiLikeC4Generator"/>).
/// </summary>
internal sealed class LikeC4CommandWorker
{
    private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

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
        using var loader = new LikeC4DocumentLoader(_logger, _httpHandler);
        if (loader.TryReadAll(_context.Sources) is not { } asyncApi || loader.TryReadAll(_context.Workflows) is not { } arazzo)
        {
            return CliExitCode.Failure;
        }

        var sources = asyncApi.Select(document => new AsyncApiLikeC4Source(document.Name, document.Content)
        {
            Link = links.GetValueOrDefault(document.Name) ?? LikeC4DocumentLoader.DefaultLink(document.Location, outputDirectory),
        }).ToList();
        var workflows = arazzo.Select(document => new ArazzoLikeC4Source(document.Name, document.Content)
        {
            Link = links.GetValueOrDefault(document.Name) ?? LikeC4DocumentLoader.DefaultLink(document.Location, outputDirectory),
        }).ToList();

        if (sources.Count == 0 && workflows.Count == 0)
        {
            _logger.Error("No AsyncAPI or Arazzo documents found.");
            return CliExitCode.Failure;
        }

        string model;
        try
        {
            model = AsyncApiLikeC4Generator.Generate(sources, workflows);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            _logger.Error(ex.Message);
            return CliExitCode.Failure;
        }

        Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(_context.OutputPath, model, Utf8WithoutBom);
        _logger.Info($"LikeC4 model of {sources.Count} AsyncAPI and {workflows.Count} Arazzo document(s) written to '{_context.OutputPath}'.");
        return CliExitCode.Success;
    }

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
}
