// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using ByteBard.AsyncAPI.Readers;
using ByteBard.AsyncAPI.Models;

namespace Bielu.AspNetCore.AsyncApi.Cli.Commands;

/// <summary>
/// Worker that validates AsyncAPI documents.
/// </summary>
internal sealed class ValidateCommandWorker
{
    private readonly ValidateCommandContext _context;
    private readonly Action<string> _writeInfo;
    private readonly Action<string> _writeWarning;
    private readonly Action<string> _writeError;

    public ValidateCommandWorker(
        ValidateCommandContext context,
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
        var files = new List<string>();
        foreach (var filePattern in _context.Files)
        {
            if (filePattern.Contains('*') || filePattern.Contains('?'))
            {
                var directory = Path.GetDirectoryName(filePattern);
                if (string.IsNullOrEmpty(directory)) directory = ".";
                var pattern = Path.GetFileName(filePattern);
                files.AddRange(Directory.GetFiles(directory, pattern, SearchOption.TopDirectoryOnly));
            }
            else
            {
                files.Add(filePattern);
            }
        }

        if (files.Count == 0)
        {
            _writeError("No files found to validate.");
            return 1;
        }

        var allDiagnostics = new List<FileDiagnostic>();
        var hasErrors = false;

        foreach (var file in files)
        {
            if (!File.Exists(file))
            {
                _writeError($"File not found: {file}");
                hasErrors = true;
                continue;
            }

            var content = File.ReadAllText(file);
            var reader = new AsyncApiStringReader();
            reader.Read(content, out var diagnostic);

            var fileDiag = new FileDiagnostic
            {
                FilePath = file,
                Errors = diagnostic.Errors.Select(e => new DiagnosticItem { Message = e.Message, Pointer = e.Pointer }).ToList(),
                Warnings = diagnostic.Warnings.Select(w => new DiagnosticItem { Message = w.Message, Pointer = w.Pointer }).ToList()
            };

            allDiagnostics.Add(fileDiag);

            if (fileDiag.Errors.Count > 0 || (_context.Strict && fileDiag.Warnings.Count > 0))
            {
                hasErrors = true;
            }
        }

        if (_context.Format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine(JsonSerializer.Serialize(allDiagnostics, new JsonSerializerOptions { WriteIndented = true }));
        }
        else
        {
            foreach (var fileDiag in allDiagnostics)
            {
                _writeInfo($"Validating {fileDiag.FilePath}...");
                foreach (var error in fileDiag.Errors)
                {
                    _writeError($"  Error: {error.Message} (at {error.Pointer})");
                }
                foreach (var warning in fileDiag.Warnings)
                {
                    if (_context.Strict)
                    {
                        _writeError($"  Warning (Strict): {warning.Message} (at {warning.Pointer})");
                    }
                    else
                    {
                        _writeWarning($"  Warning: {warning.Message} (at {warning.Pointer})");
                    }
                }

                if (fileDiag.Errors.Count == 0 && fileDiag.Warnings.Count == 0)
                {
                    _writeInfo("  OK");
                }
            }
        }

        return hasErrors ? 1 : 0;
    }

    private class FileDiagnostic
    {
        public string FilePath { get; set; } = string.Empty;
        public List<DiagnosticItem> Errors { get; set; } = [];
        public List<DiagnosticItem> Warnings { get; set; } = [];
    }

    private class DiagnosticItem
    {
        public string Message { get; set; } = string.Empty;
        public string? Pointer { get; set; }
    }
}
