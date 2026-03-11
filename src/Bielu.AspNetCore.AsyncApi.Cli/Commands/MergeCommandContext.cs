// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Bielu.AspNetCore.AsyncApi.Cli.Commands;

/// <summary>
/// Contains the context for the merge command, including all parsed command-line arguments.
/// </summary>
internal sealed class MergeCommandContext
{
    /// <summary>
    /// The document source URIs (file paths or URLs) to merge.
    /// </summary>
    public List<string> Sources { get; } = [];

    /// <summary>
    /// The output file path for the merged document.
    /// </summary>
    public string OutputPath { get; set; } = string.Empty;

    /// <summary>
    /// The title for the merged document.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// The version for the merged document.
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Key prefixes for each source, in the same order as Sources.
    /// </summary>
    public List<string?> Prefixes { get; } = [];
}
