// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Nodes;
using Bielu.Arazzo.Models;
using Bielu.Arazzo.SourceResolvers;
using Bielu.Arazzo.Validation;
using Bielu.Cli.Shared.Diagnostics;
using Bielu.Spec.Shared;
using YamlDotNet.Core;

namespace Bielu.Arazzo.Cli.Commands;

/// <summary>
/// Checks an Arazzo document's step references against the OpenAPI/AsyncAPI files its source descriptions point
/// at: the ones given with <c>--source name=path</c>, plus, with <c>--resolve-local-sources</c>, any source whose
/// <c>url</c> is a relative path to a file next to the Arazzo document.
/// </summary>
internal static class SourceReferenceChecker
{
    /// <summary>The outcome for one Arazzo file.</summary>
    /// <param name="Errors">Unresolved references, and source files that couldn't be loaded.</param>
    /// <param name="UncheckedSources">Declared sources no file was given for, whose steps were skipped.</param>
    /// <param name="UsedSources">The <c>--source</c> names this document declares, so the mapping was used.</param>
    public sealed record Result(List<DiagnosticItem> Errors, IReadOnlyList<string> UncheckedSources, IReadOnlyList<string> UsedSources);

    public static Result Check(ArazzoDocument document, string arazzoFilePath, ValidateCommandContext context)
    {
        var workspace = new ArazzoWorkspace();
        workspace.RegisterResolver(new OpenApiJsonSourceResolver());
        workspace.RegisterResolver(new AsyncApiJsonSourceResolver());

        var errors = new List<DiagnosticItem>();
        var used = new List<string>();
        var baseDirectory = Path.GetDirectoryName(Path.GetFullPath(arazzoFilePath)) ?? Directory.GetCurrentDirectory();

        for (var i = 0; i < document.SourceDescriptions.Count; i++)
        {
            var source = document.SourceDescriptions[i];
            var pointer = $"/sourceDescriptions/{i}";
            string? path = null;
            if (context.Sources.TryGetValue(source.Name, out var given))
            {
                used.Add(source.Name);
                path = given;
            }
            else if (context.ResolveLocalSources && !TryLocalPath(source.Url, baseDirectory, out path, out var pathError))
            {
                errors.Add(new DiagnosticItem($"Source '{source.Name}' url '{source.Url}' is not a usable local path: {pathError}", pointer));
                continue;
            }

            if (path is null)
            {
                continue;
            }

            if (!TryLoad(path, out var sourceDocument, out var loadError))
            {
                errors.Add(new DiagnosticItem($"Source '{source.Name}' could not be loaded from '{path}': {loadError}", pointer));
                continue;
            }

            if ((source.Type?.Value ?? SniffType(sourceDocument)) is not { } type || !workspace.TryGetResolver(type, out _))
            {
                // A local file the user didn't name, such as another Arazzo document, just stays unchecked.
                if (given is null)
                {
                    continue;
                }

                errors.Add(new DiagnosticItem(
                    $"Source '{source.Name}' ('{path}') is neither an OpenAPI nor an AsyncAPI document, so its references can't be checked.",
                    pointer));
                continue;
            }

            workspace.RegisterDocument(source.Name, type, sourceDocument);
        }

        errors.AddRange(ArazzoReferenceValidator.Validate(document, workspace)
            .Select(error => new DiagnosticItem(error.Message, error.Path)));
        return new Result(errors, ArazzoReferenceValidator.UncheckedSources(document, workspace), used);
    }

    /// <summary>
    /// The file a source url names relative to the Arazzo document (<see langword="null"/> for a URL, or a file that
    /// isn't there), or <see langword="false"/> with <paramref name="error"/> when the url is no valid path at all.
    /// </summary>
    private static bool TryLocalPath(string url, string baseDirectory, out string? path, out string error)
    {
        path = null;
        error = string.Empty;
        if (string.IsNullOrEmpty(url) || Uri.TryCreate(url, UriKind.Absolute, out var absolute) && !absolute.IsFile)
        {
            return true;
        }

        try
        {
            var full = Path.GetFullPath(Path.Combine(baseDirectory, Uri.UnescapeDataString(url)));
            path = File.Exists(full) ? full : null;
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            // Such as a NUL, which %00 decodes to.
            error = ex.Message;
            return false;
        }
    }

    private static string? SniffType(JsonNode document) => document switch
    {
        JsonObject obj when obj.ContainsKey("openapi") || obj.ContainsKey("swagger") => ArazzoSourceDescriptionType.OpenApi.Value,
        JsonObject obj when obj.ContainsKey("asyncapi") => ArazzoSourceDescriptionType.AsyncApi.Value,
        _ => null,
    };

    private static bool TryLoad(string path, out JsonNode document, out string error)
    {
        document = null!;
        error = string.Empty;
        try
        {
            var content = File.ReadAllText(path);
            var node = content.TrimStart().StartsWith('{')
                ? JsonNode.Parse(content)
                : YamlToJsonNodeConverter.Convert(new StringReader(content));
            if (node is null)
            {
                error = "the file is empty.";
                return false;
            }

            document = node;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or YamlException)
        {
            error = ex.Message;
            return false;
        }
    }
}
