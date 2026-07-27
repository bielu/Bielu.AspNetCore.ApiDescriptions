// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Bielu.Arazzo.Cli.Commands;

/// <summary>
/// Contains the context for the lint command, including all parsed command-line arguments.
/// </summary>
internal sealed class LintCommandContext
{
    /// <summary>
    /// The document files (paths or globs) to lint.
    /// </summary>
    public List<string> Files { get; } = [];

    /// <summary>
    /// Whether to treat warnings as errors.
    /// </summary>
    public bool Strict { get; set; }

    /// <summary>
    /// The output format (text or json).
    /// </summary>
    public string Format { get; set; } = "text";
}
