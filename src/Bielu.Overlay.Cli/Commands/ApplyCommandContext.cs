// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Bielu.Overlay.Cli.Commands;

/// <summary>
/// Contains the context for the apply command, including all parsed command-line arguments.
/// </summary>
internal sealed class ApplyCommandContext
{
    /// <summary>
    /// The API description to transform — OpenAPI, AsyncAPI, Arazzo, or any other JSON/YAML document.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// The overlays to apply, in the order given. Later overlays see the result of the earlier ones.
    /// </summary>
    public List<string> Overlays { get; } = [];

    /// <summary>
    /// Where to write the transformed document. When empty, it is written to standard output so the
    /// command can be piped.
    /// </summary>
    public string OutputPath { get; set; } = string.Empty;

    /// <summary>
    /// The output format (json or yaml). When empty, it is inferred from the output path's extension,
    /// falling back to json.
    /// </summary>
    public string Format { get; set; } = string.Empty;

    /// <summary>
    /// Whether a target matching zero nodes is an error rather than a warning.
    /// </summary>
    public bool Strict { get; set; }
}
