// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bielu.Cli.Shared;
using Bielu.Cli.Shared.Diff;
using ByteBard.AsyncAPI.Readers;

namespace Bielu.AspNetCore.AsyncApi.Cli.Commands;

/// <summary>
/// Worker that compares two AsyncAPI documents and reports differences.
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

        var reader = new AsyncApiStringReader();
        var baseDoc = reader.Read(baseContent, out _);
        var headDoc = reader.Read(headContent, out _);

        var comparer = new AsyncApiDocumentComparer();
        var changes = comparer.Compare(baseDoc, headDoc).ToList();

        DiffReportWriter.Write(changes, _context.Format, "AsyncAPI Diff Report", _logger);

        return DiffReportWriter.HasBreakingChanges(changes) && _context.FailOnBreaking
            ? CliExitCode.Failure
            : CliExitCode.Success;
    }
}
