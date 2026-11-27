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
    public sealed record Result(List<DiagnosticItem> Errors, IReadOnlyList<string> UncheckedSources);

    public static Result Check(ArazzoDocument document, string arazzoFilePath, ValidateCommandContext context)
    {
        var workspace = new ArazzoWorkspace();
        workspace.RegisterResolver(new OpenApiJsonSourceResolver());
        workspace.RegisterResolver(new AsyncApiJsonSourceResolver());

        var errors = new List<DiagnosticItem>();
        var baseDirectory = Path.GetDirectoryName(Path.GetFullPath(arazzoFilePath)) ?? Directory.GetCurrentDirectory();

        for (var i = 0; i < document.SourceDescriptions.Count; i++)
        {
            var source = document.SourceDescriptions[i];
            var path = context.Sources.TryGetValue(source.Name, out var given)
                ? given
                : context.ResolveLocalSources ? LocalPath(source.Url, baseDirectory) : null;
            if (path is null)
            {
                continue;
            }

            var pointer = $"/sourceDescriptions/{i}";
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
        return new Result(errors, ArazzoReferenceValidator.UncheckedSources(document, workspace));
    }

    /// <summary>A source url that names a file relative to the Arazzo document, or <see langword="null"/> for a URL.</summary>
    private static string? LocalPath(string url, string baseDirectory)
    {
        if (string.IsNullOrEmpty(url) || Uri.TryCreate(url, UriKind.Absolute, out var absolute) && !absolute.IsFile)
        {
            return null;
        }

        var path = Path.GetFullPath(Path.Combine(baseDirectory, Uri.UnescapeDataString(url)));
        return File.Exists(path) ? path : null;
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
