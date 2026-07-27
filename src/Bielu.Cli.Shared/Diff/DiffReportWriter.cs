// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using System.Text.Json;

namespace Bielu.Cli.Shared.Diff;

/// <summary>Renders <c>diff</c>-command results as text, JSON, or markdown.</summary>
public static class DiffReportWriter
{
    public static void Write(IReadOnlyList<DocumentChange> changes, string format, string reportTitle, ICliLogger logger)
    {
        if (string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine(JsonSerializer.Serialize(changes, CliJsonDefaults.Indented));
            return;
        }

        if (string.Equals(format, "markdown", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine(BuildMarkdownReport(changes, reportTitle));
            return;
        }

        if (changes.Count == 0)
        {
            logger.Info("No changes detected.");
            return;
        }

        foreach (var group in changes.GroupBy(c => c.Severity))
        {
            logger.Info($"{group.Key}:");
            foreach (var change in group)
            {
                if (group.Key == ChangeSeverity.Breaking)
                {
                    logger.Error($"  - {change.Path}: {change.Message}");
                }
                else
                {
                    logger.Info($"  - {change.Path}: {change.Message}");
                }
            }
        }
    }

    /// <summary>True if any change is <see cref="ChangeSeverity.Breaking"/>.</summary>
    public static bool HasBreakingChanges(IReadOnlyList<DocumentChange> changes) =>
        changes.Any(c => c.Severity == ChangeSeverity.Breaking);

    private static string BuildMarkdownReport(IReadOnlyList<DocumentChange> changes, string reportTitle)
    {
        if (changes.Count == 0)
        {
            return "No changes detected.";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"### {reportTitle}");
        sb.AppendLine();

        foreach (var group in changes.GroupBy(c => c.Severity))
        {
            sb.AppendLine($"#### {group.Key} Changes");
            sb.AppendLine();
            sb.AppendLine("| Path | Change |");
            sb.AppendLine("| --- | --- |");
            foreach (var change in group)
            {
                sb.AppendLine($"| `{change.Path}` | {change.Message} |");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }
}
