// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Bielu.Arazzo.Cli.Commands;

/// <summary>
/// Contains the context for the impact command, including all parsed command-line arguments.
/// </summary>
internal sealed class ImpactCommandContext
{
    /// <summary>The Arazzo document whose workflows are checked.</summary>
    public string? File { get; set; }

    /// <summary>The OpenAPI/AsyncAPI files before the change, by source-description name (<c>--base name=path</c>).</summary>
    public Dictionary<string, string> BaseSources { get; } = new(StringComparer.Ordinal);

    /// <summary>The OpenAPI/AsyncAPI files after the change, by source-description name (<c>--head name=path</c>).</summary>
    public Dictionary<string, string> HeadSources { get; } = new(StringComparer.Ordinal);

    /// <summary>Whether to exit with code 1 when a step's target no longer resolves.</summary>
    public bool FailOnBreaking { get; set; }

    /// <summary>The output format (text, json or markdown).</summary>
    public string Format { get; set; } = "text";
}
