// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bielu.Cli.Shared;
using Bielu.Cli.Shared.Diagnostics;
using ByteBard.AsyncAPI.Readers;

namespace Bielu.AspNetCore.AsyncApi.Cli.Commands;

/// <summary>
/// Worker that validates AsyncAPI documents.
/// </summary>
internal sealed class ValidateCommandWorker
{
    private readonly ValidateCommandContext _context;
    private readonly ICliLogger _logger;

    public ValidateCommandWorker(
        ValidateCommandContext context,
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
        var files = CliFileResolver.ExpandFilePatterns(_context.Files);

        if (files.Count == 0)
        {
            _logger.Error("No files found to validate.");
            return CliExitCode.Failure;
        }

        var reports = new List<FileDiagnosticReport>();

        foreach (var file in files)
        {
            if (!File.Exists(file))
            {
                _logger.Error($"File not found: {file}");
                reports.Add(new FileDiagnosticReport
                {
                    FilePath = file, Errors = [new DiagnosticItem("File not found.", null)],
                });
                continue;
            }

            string content;
            try
            {
                content = File.ReadAllText(file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Report against the file and carry on, exactly as a missing file does — one locked or
                // unreadable document in a glob must not abandon the rest of the run.
                _logger.Error($"Could not read file: {file}");
                reports.Add(new FileDiagnosticReport
                {
                    FilePath = file, Errors = [new DiagnosticItem($"Could not read file: {ex.Message}", null)],
                });
                continue;
            }

            var reader = new AsyncApiStringReader();
            reader.Read(content, out var diagnostic);

            reports.Add(new FileDiagnosticReport
            {
                FilePath = file,
                Errors = diagnostic.Errors.Select(e => new DiagnosticItem(e.Message, e.Pointer)).ToList(),
                Warnings = diagnostic.Warnings.Select(w => new DiagnosticItem(w.Message, w.Pointer)).ToList(),
            });
        }

        ValidateReportWriter.Write(reports, _context.Format, _context.Strict, _logger);

        return ValidateReportWriter.HasFailures(reports, _context.Strict) ? CliExitCode.Failure : CliExitCode.Success;
    }
}
