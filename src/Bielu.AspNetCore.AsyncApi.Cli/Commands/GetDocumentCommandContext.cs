// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Bielu.AspNetCore.AsyncApi.Cli.Commands;

/// <summary>
/// Contains the context for the get-document command, including all
/// parsed command-line arguments.
/// </summary>
internal sealed class GetDocumentCommandContext
{
    /// <summary>
    /// The name of the assembly to load.
    /// </summary>
    public string AssemblyName { get; set; } = string.Empty;

    /// <summary>
    /// The full path to the assembly to load.
    /// </summary>
    public string AssemblyPath { get; set; } = string.Empty;

    /// <summary>
    /// The output directory for generated documents.
    /// </summary>
    public string OutputDirectory { get; set; } = string.Empty;

    /// <summary>
    /// The name of the project (used for file naming).
    /// </summary>
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>
    /// The specific document name to generate. If null/empty, all documents are generated.
    /// </summary>
    public string? DocumentName { get; set; }

    /// <summary>
    /// The path to write the list of generated files.
    /// </summary>
    public string FileListPath { get; set; } = string.Empty;

    /// <summary>
    /// Override file name for the generated document (without extension).
    /// </summary>
    public string? FileName { get; set; }
}
