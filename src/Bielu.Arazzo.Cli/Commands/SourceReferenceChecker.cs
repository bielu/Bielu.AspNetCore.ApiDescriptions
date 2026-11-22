// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bielu.Arazzo.Models;
using Bielu.Arazzo.Validation;
using Bielu.Cli.Shared.Diagnostics;

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
        var errors = new List<DiagnosticItem>();
        var used = document.SourceDescriptions
            .Select(source => source.Name)
            .Where(context.Sources.ContainsKey)
            .ToList();
        var localBaseDirectory = context.ResolveLocalSources
            ? Path.GetDirectoryName(Path.GetFullPath(arazzoFilePath)) ?? Directory.GetCurrentDirectory()
            : null;
        var workspace = SourceWorkspaceBuilder.Build(document, context.Sources, localBaseDirectory, errors);

        try
        {
            errors.AddRange(ArazzoReferenceValidator.Validate(document, workspace)
                .Select(error => new DiagnosticItem(error.Message, error.Path)));
        }
        catch (InvalidOperationException ex)
        {
            // The resolvers skip values of the wrong kind, but a source file is user input: one shaped in a way they
            // don't expect is reported against this document rather than ending the run.
            errors.Add(new DiagnosticItem($"The step references could not be checked against the source documents: {ex.Message}", null));
        }

        return new Result(errors, ArazzoReferenceValidator.UncheckedSources(document, workspace), used);
    }
}
