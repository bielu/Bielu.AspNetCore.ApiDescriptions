// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Bielu.Cli.Shared.Diagnostics;

/// <summary>The errors and warnings found while validating a single document file.</summary>
public sealed class FileDiagnosticReport
{
    public required string FilePath { get; init; }

    public List<DiagnosticItem> Errors { get; init; } = [];

    public List<DiagnosticItem> Warnings { get; init; } = [];
}
