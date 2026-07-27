// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bielu.Arazzo.Readers;
using Bielu.Cli.Shared;
using Bielu.Cli.Shared.Diagnostics;

namespace Bielu.Arazzo.Cli.Commands;

/// <summary>
/// Worker that lints Arazzo documents via <see cref="ArazzoLinter"/> (style and graph-shape checks, as
/// opposed to <c>validate</c>'s structural invariants).
/// </summary>
internal sealed class LintCommandWorker
{
    private readonly LintCommandContext _context;
    private readonly ICliLogger _logger;

    public LintCommandWorker(
        LintCommandContext context,
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
            _logger.Error("No files found to lint.");
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

            reports.Add(LintFile(file));
        }

        ValidateReportWriter.Write(reports, _context.Format, _context.Strict, _logger, verb: "Linting");

        return ValidateReportWriter.HasFailures(reports, _context.Strict) ? CliExitCode.Failure : CliExitCode.Success;
    }

    private static FileDiagnosticReport LintFile(string file)
    {
        var result = ArazzoStringReader.Read(File.ReadAllText(file));

        if (result.Document is null)
        {
            return new FileDiagnosticReport
            {
                FilePath = file,
                Errors = result.Diagnostics.Errors.Select(e => new DiagnosticItem(e.Message, e.Path)).ToList(),
            };
        }

        var findings = ArazzoLinter.Lint(result.Document);

        return new FileDiagnosticReport
        {
            FilePath = file,
            Errors = findings.Where(f => !f.IsWarning).Select(f => new DiagnosticItem(f.Message, f.Path)).ToList(),
            Warnings = findings.Where(f => f.IsWarning).Select(f => new DiagnosticItem(f.Message, f.Path)).ToList(),
        };
    }
}
