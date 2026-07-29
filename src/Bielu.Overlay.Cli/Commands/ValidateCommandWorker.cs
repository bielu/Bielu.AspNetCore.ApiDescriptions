// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bielu.Cli.Shared;
using Bielu.Cli.Shared.Diagnostics;
using Bielu.Overlay.Readers;
using Bielu.Overlay.Validation;

namespace Bielu.Overlay.Cli.Commands;

/// <summary>
/// Worker that validates overlay documents on their own terms: reader diagnostics plus
/// <see cref="OverlayValidator"/>'s structural checks. No target document is involved, which is the point
/// — an overlay can be checked in CI without the description it will eventually transform.
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
        _context = context ?? throw new ArgumentNullException(nameof(context));
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
                    FilePath = file,
                    Errors = [new DiagnosticItem("File not found.", null)],
                });
                continue;
            }

            reports.Add(ValidateFile(file));
        }

        ValidateReportWriter.Write(reports, _context.Format, _context.Strict, _logger);

        return ValidateReportWriter.HasFailures(reports, _context.Strict) ? CliExitCode.Failure : CliExitCode.Success;
    }

    private static FileDiagnosticReport ValidateFile(string file)
    {
        var result = OverlayStringReader.Read(File.ReadAllText(file));

        var errors = result.Diagnostics.Where(d => !d.IsWarning).Select(d => new DiagnosticItem(d.Message, d.Path)).ToList();
        var warnings = result.Diagnostics.Where(d => d.IsWarning).Select(d => new DiagnosticItem(d.Message, d.Path)).ToList();

        if (result.Document is { } overlay)
        {
            foreach (var finding in OverlayValidator.Validate(overlay))
            {
                var item = new DiagnosticItem(finding.Message, finding.Path);
                if (finding.IsWarning)
                {
                    warnings.Add(item);
                }
                else
                {
                    errors.Add(item);
                }
            }
        }

        return new FileDiagnosticReport { FilePath = file, Errors = errors, Warnings = warnings };
    }
}
