// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;

namespace Bielu.Cli.Shared.Diagnostics;

/// <summary>Renders <c>validate</c>-command results as text or JSON.</summary>
public static class ValidateReportWriter
{
    public static void Write(IReadOnlyList<FileDiagnosticReport> reports, string format, bool strict, ICliLogger logger,
        string verb = "Validating")
    {
        if (string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine(JsonSerializer.Serialize(reports, CliJsonDefaults.Indented));
            return;
        }

        foreach (var report in reports)
        {
            logger.Info($"{verb} {report.FilePath}...");

            foreach (var error in report.Errors)
            {
                logger.Error($"  Error: {error.Message} (at {error.Pointer})");
            }

            foreach (var warning in report.Warnings)
            {
                if (strict)
                {
                    logger.Error($"  Warning (Strict): {warning.Message} (at {warning.Pointer})");
                }
                else
                {
                    logger.Warning($"  Warning: {warning.Message} (at {warning.Pointer})");
                }
            }

            if (report.Errors.Count == 0 && report.Warnings.Count == 0)
            {
                logger.Info("  OK");
            }
        }
    }

    /// <summary>True if any report has errors, or (in <paramref name="strict"/> mode) any warnings.</summary>
    public static bool HasFailures(IReadOnlyList<FileDiagnosticReport> reports, bool strict) =>
        reports.Any(r => r.Errors.Count > 0 || (strict && r.Warnings.Count > 0));
}
