// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using System.Text.Json;
using ByteBard.AsyncAPI.Readers;

namespace Bielu.AspNetCore.AsyncApi.Cli.Commands;

/// <summary>
/// Worker that compares two AsyncAPI documents and reports differences.
/// </summary>
internal sealed class DiffCommandWorker
{
    private readonly DiffCommandContext _context;
    private readonly Action<string> _writeInfo;
    private readonly Action<string> _writeWarning;
    private readonly Action<string> _writeError;

    public DiffCommandWorker(
        DiffCommandContext context,
        Action<string> writeInfo,
        Action<string> writeWarning,
        Action<string> writeError)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _writeInfo = writeInfo;
        _writeWarning = writeWarning;
        _writeError = writeError;
    }

    public int Process()
    {
        if (!File.Exists(_context.BasePath))
        {
            _writeError($"Base file not found: {_context.BasePath}");
            return 1;
        }

        if (!File.Exists(_context.HeadPath))
        {
            _writeError($"Head file not found: {_context.HeadPath}");
            return 1;
        }

        var baseContent = File.ReadAllText(_context.BasePath);
        var headContent = File.ReadAllText(_context.HeadPath);

        var reader = new AsyncApiStringReader();
        var baseDoc = reader.Read(baseContent, out _);
        var headDoc = reader.Read(headContent, out _);

        var comparer = new AsyncApiDocumentComparer();
        var changes = comparer.Compare(baseDoc, headDoc).ToList();

        var hasBreaking = changes.Any(c => c.Severity == ChangeSeverity.Breaking);

        if (_context.Format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine(JsonSerializer.Serialize(changes, new JsonSerializerOptions { WriteIndented = true }));
        }
        else if (_context.Format.Equals("markdown", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine(BuildMarkdownReport(changes));
        }
        else
        {
            if (changes.Count == 0)
            {
                _writeInfo("No changes detected.");
            }
            else
            {
                foreach (var group in changes.GroupBy(c => c.Severity))
                {
                    _writeInfo($"{group.Key}:");
                    foreach (var change in group)
                    {
                        if (group.Key == ChangeSeverity.Breaking)
                            _writeError($"  - {change.Path}: {change.Message}");
                        else
                            _writeInfo($"  - {change.Path}: {change.Message}");
                    }
                }
            }
        }

        return (hasBreaking && _context.FailOnBreaking) ? 1 : 0;
    }

    private string BuildMarkdownReport(List<AsyncApiChange> changes)
    {
        if (changes.Count == 0)
        {
            return "No changes detected.";
        }

        var sb = new StringBuilder();
        sb.AppendLine("### AsyncAPI Diff Report");
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
