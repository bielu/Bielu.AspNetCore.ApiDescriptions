// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Bielu.Cli.Shared;

/// <summary>
/// The standard <c>info: </c>/<c>warn: </c>/<c>error: </c> console logger used by every Bielu CLI tool.
/// Info and warning messages go to stdout, errors to stderr.
/// </summary>
public sealed class ConsoleCliLogger : ICliLogger
{
    public void Info(string message) => Console.WriteLine($"info: {message}");

    public void Warning(string message) => Console.WriteLine($"warn: {message}");

    public void Error(string message) => Console.Error.WriteLine($"error: {message}");
}
