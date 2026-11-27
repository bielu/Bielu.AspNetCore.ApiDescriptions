// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Bielu.Arazzo.Cli.Commands;

/// <summary>
/// Contains the context for the validate command, including all parsed command-line arguments.
/// </summary>
internal sealed class ValidateCommandContext
{
    /// <summary>
    /// The document files (paths or globs) to validate.
    /// </summary>
    public List<string> Files { get; } = [];

    /// <summary>
    /// Whether to treat warnings as errors.
    /// </summary>
    public bool Strict { get; set; }

    /// <summary>
    /// The OpenAPI/AsyncAPI files to check step references against, by source-description name
    /// (<c>--source name=path</c>). When empty and <see cref="ResolveLocalSources"/> is off, references aren't checked.
    /// </summary>
    public Dictionary<string, string> Sources { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Whether a source description whose <c>url</c> is a relative path to an existing file is loaded from it
    /// (<c>--resolve-local-sources</c>), so its step references are checked.
    /// </summary>
    public bool ResolveLocalSources { get; set; }

    /// <summary>
    /// The output format (text or json).
    /// </summary>
    public string Format { get; set; } = "text";
}
