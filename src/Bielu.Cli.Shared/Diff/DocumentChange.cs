// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Bielu.Cli.Shared.Diff;

/// <summary>A single addition/removal/modification found while comparing two document versions.</summary>
public sealed record DocumentChange(string Path, string Message, ChangeSeverity Severity);
