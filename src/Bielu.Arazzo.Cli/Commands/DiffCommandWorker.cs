// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bielu.Arazzo.Readers;
using Bielu.Cli.Shared;
using Bielu.Cli.Shared.Diff;

namespace Bielu.Arazzo.Cli.Commands;

/// <summary>
/// Worker that compares two Arazzo documents and reports differences.
/// </summary>
internal sealed class DiffCommandWorker
{
    private readonly DiffCommandContext _context;
    private readonly ICliLogger _logger;

    public DiffCommandWorker(
        DiffCommandContext context,
        Action<string> writeInfo,
        Action<string> writeWarning,
        Action<string> writeError)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(writeInfo);
        ArgumentNullException.ThrowIfNull(writeWarning);
        ArgumentNullException.ThrowIfNull(writeError);

        _context = context;
        _logger = new DelegatingCliLogger(writeInfo, writeWarning, writeError);
    }

    public int Process()
    {
        if (!File.Exists(_context.BasePath))
        {
            _logger.Error($"Base file not found: {_context.BasePath}");
            return CliExitCode.Failure;
        }

        if (!File.Exists(_context.HeadPath))
        {
            _logger.Error($"Head file not found: {_context.HeadPath}");
            return CliExitCode.Failure;
        }

        string baseContent;
        string headContent;
        try
        {
            // File.Exists above does not rule out a file that is locked or unreadable.
            baseContent = File.ReadAllText(_context.BasePath);
            headContent = File.ReadAllText(_context.HeadPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.Error($"Failed to read input: {ex.Message}");
            return CliExitCode.Failure;
        }

        var baseResult = ArazzoStringReader.Read(baseContent);
        var headResult = ArazzoStringReader.Read(headContent);

        if (baseResult.Document is null || headResult.Document is null)
        {
            _logger.Error("Unable to parse one or both documents.");
            return CliExitCode.Failure;
        }

        var comparer = new ArazzoDocumentComparer();
        var changes = comparer.Compare(baseResult.Document, headResult.Document).ToList();

        DiffReportWriter.Write(changes, _context.Format, "Arazzo Diff Report", _logger);

        return DiffReportWriter.HasBreakingChanges(changes) && _context.FailOnBreaking
            ? CliExitCode.Failure
            : CliExitCode.Success;
    }
}
