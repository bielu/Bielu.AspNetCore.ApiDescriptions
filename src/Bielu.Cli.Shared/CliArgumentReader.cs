// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Bielu.Cli.Shared;

/// <summary>Reads the value following a <c>--flag value</c> style command-line option.</summary>
public static class CliArgumentReader
{
    /// <summary>
    /// Advances <paramref name="index"/> to the next argument and returns it as <paramref name="value"/>.
    /// Logs "<paramref name="optionName"/> requires a value." and returns <see langword="false"/> if
    /// <paramref name="index"/> was already at the last argument.
    /// </summary>
    public static bool TryReadValue(string[] args, ref int index, string optionName, ICliLogger logger, out string value)
    {
        if (++index >= args.Length)
        {
            logger.Error($"{optionName} requires a value.");
            value = string.Empty;
            return false;
        }

        value = args[index];
        return true;
    }
}
