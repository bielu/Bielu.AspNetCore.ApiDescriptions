// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Bielu.Cli.Shared;

/// <summary>Expands <c>--file</c>-style path/glob arguments into concrete file paths.</summary>
public static class CliFileResolver
{
    /// <summary>
    /// Expands each pattern: a literal path is passed through unchanged (even if it doesn't exist, so
    /// callers can report a clear "file not found"), while a pattern containing <c>*</c> or <c>?</c> is
    /// resolved against its directory (or the current directory, if none is given).
    /// </summary>
    public static List<string> ExpandFilePatterns(IEnumerable<string> patterns)
    {
        var files = new List<string>();
        foreach (var pattern in patterns)
        {
            if (pattern.Contains('*') || pattern.Contains('?'))
            {
                var directory = Path.GetDirectoryName(pattern);
                if (string.IsNullOrEmpty(directory))
                {
                    directory = ".";
                }

                var fileNamePattern = Path.GetFileName(pattern);
                files.AddRange(Directory.GetFiles(directory, fileNamePattern, SearchOption.TopDirectoryOnly));
            }
            else
            {
                files.Add(pattern);
            }
        }

        return files;
    }
}
