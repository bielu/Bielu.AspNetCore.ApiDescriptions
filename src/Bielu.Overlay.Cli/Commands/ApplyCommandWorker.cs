// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bielu.Cli.Shared;
using Bielu.Overlay.Readers;
using Bielu.Spec.Shared;

namespace Bielu.Overlay.Cli.Commands;

/// <summary>
/// Worker that applies one or more overlays to an API description.
/// </summary>
/// <remarks>
/// The document is read as a <see cref="JsonNode"/> tree whatever its source format, so OpenAPI, AsyncAPI
/// and Arazzo descriptions are all valid inputs. Overlays are applied in the order given, each against the
/// result of the last — the same sequencing the specification requires of actions within one overlay.
/// </remarks>
internal sealed class ApplyCommandWorker
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private readonly ApplyCommandContext _context;
    private readonly ICliLogger _logger;

    public ApplyCommandWorker(
        ApplyCommandContext context,
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
        if (!File.Exists(_context.FilePath))
        {
            _logger.Error($"File not found: {_context.FilePath}");
            return CliExitCode.Failure;
        }

        if (ReadDocument(_context.FilePath) is not { } document)
        {
            return CliExitCode.Failure;
        }

        var applyOptions = new OverlayApplyOptions { Strict = _context.Strict };
        var hasErrors = false;

        foreach (var overlayPath in _context.Overlays)
        {
            if (!File.Exists(overlayPath))
            {
                _logger.Error($"Overlay not found: {overlayPath}");
                return CliExitCode.Failure;
            }

            OverlayReadResult read;
            try
            {
                read = OverlayStringReader.Read(File.ReadAllText(overlayPath));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A file that exists but is locked or unreadable should report like any other input
                // failure, not surface a stack trace from the middle of a build step.
                _logger.Error($"Failed to read {overlayPath}: {ex.Message}");
                return CliExitCode.Failure;
            }

            ReportDiagnostics(overlayPath, read.Diagnostics);

            if (read.Document is not { } overlay)
            {
                _logger.Error($"Overlay could not be read: {overlayPath}");
                return CliExitCode.Failure;
            }

            if (read.HasErrors)
            {
                hasErrors = true;
                continue;
            }

            var result = OverlayApplier.Apply(document, overlay, applyOptions);
            ReportDiagnostics(overlayPath, result.Diagnostics);

            if (result.HasErrors)
            {
                hasErrors = true;
            }

            // Feed the transformed tree into the next overlay even when this one reported problems, so a
            // run surfaces every failure at once rather than one per invocation.
            document = result.Document ?? document;
        }

        if (hasErrors)
        {
            _logger.Error("Overlay application reported errors; output was not written.");
            return CliExitCode.Failure;
        }

        return WriteDocument(document);
    }

    private JsonNode? ReadDocument(string path)
    {
        var content = File.ReadAllText(path);

        try
        {
            // Same JSON-first-then-YAML strategy the readers use: the formats overlap at the opening
            // brace, so a YAML flow mapping must not be written off when JSON parsing fails.
            if (LooksLikeJson(content))
            {
                try
                {
                    return JsonNode.Parse(content);
                }
                catch (JsonException)
                {
                    // Not JSON after all — fall through to YAML.
                }
            }

            return YamlToJsonNodeConverter.Convert(new StringReader(content));
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to parse {path}: {ex.Message}");
            return null;
        }
    }

    private int WriteDocument(JsonNode document)
    {
        var serialized = ResolveFormat() == "yaml"
            ? JsonNodeToYamlConverter.Serialize(document)
            : document.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

        if (string.IsNullOrEmpty(_context.OutputPath))
        {
            Console.WriteLine(serialized);
            return CliExitCode.Success;
        }

        try
        {
            var directory = Path.GetDirectoryName(_context.OutputPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_context.OutputPath, serialized, Utf8NoBom);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.Error($"Failed to write {_context.OutputPath}: {ex.Message}");
            return CliExitCode.Failure;
        }

        _logger.Info($"Transformed document written to '{_context.OutputPath}'.");
        return CliExitCode.Success;
    }

    /// <summary>
    /// Explicit <c>--format</c> wins; otherwise the output path's extension decides, matching how the
    /// AsyncAPI <c>merge</c> command picks its format. Writing to stdout defaults to JSON.
    /// </summary>
    private string ResolveFormat()
    {
        if (!string.IsNullOrEmpty(_context.Format))
        {
            return _context.Format.ToLowerInvariant();
        }

        var isYamlPath = _context.OutputPath.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase)
                         || _context.OutputPath.EndsWith(".yml", StringComparison.OrdinalIgnoreCase);

        return isYamlPath ? "yaml" : "json";
    }

    private void ReportDiagnostics(string overlayPath, IReadOnlyList<OverlayDiagnostic> diagnostics)
    {
        foreach (var diagnostic in diagnostics)
        {
            var message = $"{overlayPath}{diagnostic.Path}: {diagnostic.Message}";
            if (diagnostic.IsWarning)
            {
                _logger.Warning(message);
            }
            else
            {
                _logger.Error(message);
            }
        }
    }

    private static bool LooksLikeJson(string content)
    {
        foreach (var c in content)
        {
            if (char.IsWhiteSpace(c))
            {
                continue;
            }

            return c is '{' or '[';
        }

        return false;
    }
}
