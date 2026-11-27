// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Nodes;
using Bielu.Arazzo.Models;
using Bielu.Arazzo.SourceResolvers;
using Bielu.Cli.Shared.Diagnostics;
using Bielu.Spec.Shared;
using YamlDotNet.Core;

namespace Bielu.Arazzo.Cli.Commands;

/// <summary>
/// Builds an <see cref="ArazzoWorkspace"/> for one Arazzo document from OpenAPI/AsyncAPI files: the ones named per
/// source description, plus, when a base directory is given, any source whose <c>url</c> is a relative path to a
/// file in it. Shared by <c>validate --source</c> and <c>impact</c>.
/// </summary>
internal static class SourceWorkspaceBuilder
{
    /// <summary>Builds the workspace, appending a diagnostic to <paramref name="errors"/> for each file that can't be used.</summary>
    /// <param name="document">The Arazzo document whose source descriptions are matched.</param>
    /// <param name="sources">The files given by name (<c>--source</c>, <c>--base</c>, <c>--head</c>).</param>
    /// <param name="localBaseDirectory">The folder relative source urls are resolved from, or <see langword="null"/> not to resolve them.</param>
    /// <param name="errors">Receives a diagnostic for each file that can't be loaded or isn't an OpenAPI/AsyncAPI document.</param>
    public static ArazzoWorkspace Build(
        ArazzoDocument document,
        IReadOnlyDictionary<string, string> sources,
        string? localBaseDirectory,
        List<DiagnosticItem> errors)
    {
        var workspace = new ArazzoWorkspace();
        workspace.RegisterResolver(new OpenApiJsonSourceResolver());
        workspace.RegisterResolver(new AsyncApiJsonSourceResolver());

        for (var i = 0; i < document.SourceDescriptions.Count; i++)
        {
            var source = document.SourceDescriptions[i];
            var path = sources.TryGetValue(source.Name, out var given)
                ? given
                : localBaseDirectory is not null ? LocalPath(source.Url, localBaseDirectory) : null;
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

        return workspace;
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
