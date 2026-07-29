// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Bielu.Arazzo.Cli.Commands;

/// <summary>
/// Contains the context for the diff command, including all parsed command-line arguments.
/// </summary>
internal sealed class DiffCommandContext
{
    /// <summary>
    /// The base (old) Arazzo document path.
    /// </summary>
    public string BasePath { get; set; } = string.Empty;

    /// <summary>
    /// The head (new) Arazzo document path.
    /// </summary>
    public string HeadPath { get; set; } = string.Empty;

    /// <summary>
    /// Whether to fail (exit 1) if breaking changes are detected.
    /// </summary>
    public bool FailOnBreaking { get; set; }

    /// <summary>
    /// The output format (text, json, or markdown).
    /// </summary>
    public string Format { get; set; } = "text";
}
