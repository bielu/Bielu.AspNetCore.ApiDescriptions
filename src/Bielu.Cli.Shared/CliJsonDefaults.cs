// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;

namespace Bielu.Cli.Shared;

/// <summary>The <c>--format json</c> serialization options shared by every Bielu CLI report.</summary>
public static class CliJsonDefaults
{
    public static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };
}
