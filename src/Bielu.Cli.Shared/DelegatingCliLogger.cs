// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Bielu.Cli.Shared;

/// <summary>
/// Adapts the <c>Action&lt;string&gt; writeInfo/writeWarning/writeError</c> triple used by existing command
/// worker constructors to <see cref="ICliLogger"/>, so those workers can consume the shared report writers
/// without changing their public constructor shape (and breaking callers/tests built around it).
/// </summary>
public sealed class DelegatingCliLogger : ICliLogger
{
    private readonly Action<string> _writeInfo;
    private readonly Action<string> _writeWarning;
    private readonly Action<string> _writeError;

    public DelegatingCliLogger(Action<string> writeInfo, Action<string> writeWarning, Action<string> writeError)
    {
        _writeInfo = writeInfo;
        _writeWarning = writeWarning;
        _writeError = writeError;
    }

    public void Info(string message) => _writeInfo(message);

    public void Warning(string message) => _writeWarning(message);

    public void Error(string message) => _writeError(message);
}
