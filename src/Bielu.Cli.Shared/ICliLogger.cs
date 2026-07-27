// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Bielu.Cli.Shared;

/// <summary>
/// Writes info/warning/error messages from a CLI command worker, decoupling the worker from
/// <see cref="Console"/> so it can be unit tested with a fake logger.
/// </summary>
public interface ICliLogger
{
    void Info(string message);

    void Warning(string message);

    void Error(string message);
}
