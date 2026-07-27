// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Bielu.Cli.Shared.Diagnostics;

/// <summary>A single validation finding, located by a JSON-Pointer-style path from the document root.</summary>
public sealed record DiagnosticItem(string Message, string? Pointer);
