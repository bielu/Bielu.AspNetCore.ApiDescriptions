// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bielu.Arazzo.Readers;
using Bielu.Arazzo.Validation;
using Bielu.Cli.Shared;
using Bielu.Cli.Shared.Diagnostics;

namespace Bielu.Arazzo.Cli.Commands;

/// <summary>
/// Worker that validates Arazzo documents: reader diagnostics (malformed JSON/YAML, unrecognized version)
/// plus <see cref="ArazzoValidator"/>'s structural invariants (duplicate ids, mutually exclusive step
/// targets, unknown enum values, ...).
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
        var result = ArazzoStringReader.Read(File.ReadAllText(file));

        var errors = result.Diagnostics.Errors.Select(e => new DiagnosticItem(e.Message, e.Path)).ToList();
        var warnings = result.Diagnostics.Warnings.Select(w => new DiagnosticItem(w.Message, w.Path)).ToList();

        if (result.Document is not null)
        {
            foreach (var finding in ArazzoValidator.Validate(result.Document))
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
