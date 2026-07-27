// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bielu.Cli.Shared;

namespace Bielu.Cli.Shared.Tests;

internal sealed class FakeCliLogger : ICliLogger
{
    public List<string> InfoMessages { get; } = [];

    public List<string> WarningMessages { get; } = [];

    public List<string> ErrorMessages { get; } = [];

    public void Info(string message) => InfoMessages.Add(message);

    public void Warning(string message) => WarningMessages.Add(message);

    public void Error(string message) => ErrorMessages.Add(message);
}
