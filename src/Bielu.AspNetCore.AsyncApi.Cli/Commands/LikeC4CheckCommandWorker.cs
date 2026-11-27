using Bielu.AspNetCore.AsyncApi.LikeC4;
using Bielu.Cli.Shared;
using Bielu.Cli.Shared.Diagnostics;

namespace Bielu.AspNetCore.AsyncApi.Cli.Commands;

/// <summary>
/// Worker that checks a LikeC4 model against the AsyncAPI documents it describes (see <see cref="LikeC4ModelChecker"/>),
/// reporting the way <c>validate</c> does.
/// </summary>
internal sealed class LikeC4CheckCommandWorker
{
    private readonly LikeC4CheckCommandContext _context;
    private readonly ICliLogger _logger;
    private readonly HttpMessageHandler? _httpHandler;

    /// <param name="context">The parsed arguments.</param>
    /// <param name="writeInfo">Writes a line of progress.</param>
    /// <param name="writeWarning">Writes a warning.</param>
    /// <param name="writeError">Writes an error.</param>
    /// <param name="httpHandler">The handler documents given by URL are fetched through; the default one when <see langword="null"/>.</param>
    public LikeC4CheckCommandWorker(
        LikeC4CheckCommandContext context,
        Action<string> writeInfo,
        Action<string> writeWarning,
        Action<string> writeError,
        HttpMessageHandler? httpHandler = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(writeInfo);
        ArgumentNullException.ThrowIfNull(writeWarning);
        ArgumentNullException.ThrowIfNull(writeError);

        _context = context;
        _logger = new DelegatingCliLogger(writeInfo, writeWarning, writeError);
        _httpHandler = httpHandler;
    }

    public int Process()
    {
        using var loader = new LikeC4DocumentLoader(_logger, _httpHandler);
        if (!loader.TryRead(_context.ModelPath, out var model) || loader.TryReadAll(_context.Sources) is not { } documents)
        {
            return CliExitCode.Failure;
        }

        if (documents.Count == 0)
        {
            _logger.Error("No AsyncAPI documents found.");
            return CliExitCode.Failure;
        }

        IReadOnlyList<LikeC4CheckFinding> findings;
        try
        {
            findings = LikeC4ModelChecker.Check(model, documents.Select(document => new AsyncApiLikeC4Source(document.Name, document.Content)));
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            _logger.Error(ex.Message);
            return CliExitCode.Failure;
        }

        var report = new FileDiagnosticReport
        {
            FilePath = _context.ModelPath,
            Errors = Items(findings, LikeC4CheckSeverity.Error),
            Warnings = Items(findings, LikeC4CheckSeverity.Warning),
        };
        ValidateReportWriter.Write([report], _context.Format, _context.Strict, _logger, verb: "Checking");
        return ValidateReportWriter.HasFailures([report], _context.Strict) ? CliExitCode.Failure : CliExitCode.Success;
    }

    private static List<DiagnosticItem> Items(IReadOnlyList<LikeC4CheckFinding> findings, LikeC4CheckSeverity severity) =>
        findings
            .Where(finding => finding.Severity == severity)
            .Select(finding => new DiagnosticItem(finding.Message, finding.Location ?? "model"))
            .ToList();
}
