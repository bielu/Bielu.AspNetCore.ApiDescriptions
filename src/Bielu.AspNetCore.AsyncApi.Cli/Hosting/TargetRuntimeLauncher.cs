// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text.Json;
using Bielu.AspNetCore.AsyncApi.Cli.Commands;
using Bielu.Cli.Shared;

namespace Bielu.AspNetCore.AsyncApi.Cli.Hosting;

/// <summary>
/// Runs <c>getdocument</c> on the runtime the target application was built for.
/// </summary>
/// <remarks>
/// <para>
/// <c>getdocument</c> boots the application inside the CLI's process. That only works when both use the
/// same .NET runtime: a CLI on .NET 10 cannot load a net11.0 application (its <c>System.Runtime</c>
/// 11.0.0.0 does not exist there). Running a net10.0 application on .NET 11 would load, but it would
/// generate the document on a runtime the application never runs on.
/// </para>
/// <para>
/// When the runtimes differ, the CLI relaunches itself the way <c>dotnet getdocument</c> does:
/// <c>dotnet exec --depsfile app.deps.json --runtimeconfig app.runtimeconfig.json</c>, running the
/// CLI build for the highest target framework the tool ships that does not exceed the application's. When they match, the
/// CLI runs in-process exactly as before. Arguments, output and exit codes are the same either way: the
/// relaunched worker receives the original arguments, and its exit code becomes the CLI's.
/// </para>
/// </remarks>
internal static class TargetRuntimeLauncher
{
    /// <summary>
    /// Set on the relaunched worker so it runs the command in-process instead of relaunching again.
    /// </summary>
    internal const string WorkerEnvironmentVariable = "BIELU_ASYNCAPI_CLI_WORKER";

    /// <summary>
    /// Relaunches <c>getdocument</c> on the application's runtime when it differs from the current one.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when the command was handled here, with its exit code in
    /// <paramref name="exitCode"/>; <see langword="false"/> when the caller should run it in-process.
    /// </returns>
    public static bool TryRelaunch(GetDocumentCommandContext context, string[] args, ICliLogger logger, out int exitCode)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(logger);

        exitCode = CliExitCode.Success;
        if (Environment.GetEnvironmentVariable(WorkerEnvironmentVariable) == "1"
            || string.IsNullOrEmpty(context.AssemblyPath))
        {
            return false;
        }

        var assemblyPath = Path.GetFullPath(context.AssemblyPath);
        var runtimeConfigPath = Path.ChangeExtension(assemblyPath, ".runtimeconfig.json");
        var depsPath = Path.ChangeExtension(assemblyPath, ".deps.json");
        if (!File.Exists(runtimeConfigPath) || !File.Exists(depsPath)
            || !TryReadTargetFramework(runtimeConfigPath, out var targetFramework))
        {
            // Not a framework-dependent application we can describe; keep the in-process behaviour.
            return false;
        }

        var currentRuntime = new Version(Environment.Version.Major, Environment.Version.Minor);
        if (targetFramework == currentRuntime)
        {
            return false;
        }

        var cliAssembly = typeof(TargetRuntimeLauncher).Assembly;
        var cliDirectory = Path.GetDirectoryName(cliAssembly.Location)!;
        var workerFileName = cliAssembly.GetName().Name + ".dll";
        var workerPath = FindWorkerAssembly(cliDirectory, workerFileName, targetFramework);
        if (workerPath is null)
        {
            logger.Error(
                $"'{context.AssemblyName}' targets net{targetFramework}, which this version of the AsyncAPI CLI " +
                $"does not support. It ships builds for: {string.Join(", ", FindAvailableTargetFrameworks(cliDirectory, workerFileName).Select(v => $"net{v}"))}.");
            exitCode = CliExitCode.Failure;
            return true;
        }

        logger.Info($"'{context.AssemblyName}' targets net{targetFramework}; generating on that runtime.");
        var startInfo = CreateStartInfo(ResolveMuxerPath(), depsPath, runtimeConfigPath, workerPath, args);
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Could not start '{startInfo.FileName}'.");
        process.WaitForExit();

        // The worker reports its own failures, and the host reports a missing runtime itself
        // ("You must install or update .NET ..."), so the exit code is all that is left to pass on.
        exitCode = process.ExitCode;
        return true;
    }

    /// <summary>
    /// Reads the target framework (<c>runtimeOptions.tfm</c>, e.g. <c>net11.0</c>) from a runtimeconfig.json.
    /// </summary>
    internal static bool TryReadTargetFramework(string runtimeConfigPath, out Version targetFramework)
    {
        ArgumentNullException.ThrowIfNull(runtimeConfigPath);

        targetFramework = new Version(0, 0);
        try
        {
            using var stream = File.OpenRead(runtimeConfigPath);
            using var document = JsonDocument.Parse(stream);
            return document.RootElement.TryGetProperty("runtimeOptions", out var runtimeOptions)
                && runtimeOptions.TryGetProperty("tfm", out var tfm)
                && tfm.ValueKind == JsonValueKind.String
                && TryParseTargetFramework(tfm.GetString(), out targetFramework);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Parses a .NET target framework moniker such as <c>net11.0</c>; rejects <c>netstandard</c> and
    /// .NET Framework monikers.
    /// </summary>
    internal static bool TryParseTargetFramework(string? moniker, out Version version)
    {
        version = new Version(0, 0);
        if (moniker is null
            || !moniker.StartsWith("net", StringComparison.OrdinalIgnoreCase)
            || !moniker.Contains('.')
            || !Version.TryParse(moniker[3..], out var parsed)
            || parsed.Major < 5)
        {
            return false;
        }

        version = new Version(parsed.Major, parsed.Minor);
        return true;
    }

    /// <summary>
    /// Finds the CLI build to run for an application targeting <paramref name="targetFramework"/>: the
    /// highest framework not above it. Works for both the tool package layout
    /// (<c>tools/&lt;tfm&gt;/any/</c>) and a build output layout (<c>bin/&lt;config&gt;/&lt;tfm&gt;/</c>).
    /// </summary>
    internal static string? FindWorkerAssembly(string cliDirectory, string workerFileName, Version targetFramework)
    {
        ArgumentNullException.ThrowIfNull(cliDirectory);
        ArgumentNullException.ThrowIfNull(workerFileName);
        ArgumentNullException.ThrowIfNull(targetFramework);

        return EnumerateWorkers(cliDirectory, workerFileName)
            .Where(worker => worker.TargetFramework <= targetFramework)
            .OrderByDescending(worker => worker.TargetFramework)
            .Select(worker => worker.Path)
            .FirstOrDefault();
    }

    internal static IReadOnlyList<Version> FindAvailableTargetFrameworks(string cliDirectory, string workerFileName) =>
        EnumerateWorkers(cliDirectory, workerFileName).Select(worker => worker.TargetFramework).Order().ToList();

    private static IEnumerable<(Version TargetFramework, string Path)> EnumerateWorkers(string cliDirectory, string workerFileName)
    {
        var current = new DirectoryInfo(Path.TrimEndingDirectorySeparator(Path.GetFullPath(cliDirectory)));

        // tools/<tfm>/any/ in a tool package; <tfm>/ in a build output folder.
        var leaf = string.Equals(current.Name, "any", StringComparison.OrdinalIgnoreCase) ? current.Name : null;
        var frameworkDirectory = leaf is null ? current : current.Parent;
        var root = frameworkDirectory?.Parent;
        if (root is null || !root.Exists)
        {
            yield break;
        }

        foreach (var directory in root.EnumerateDirectories("net*"))
        {
            if (!TryParseTargetFramework(directory.Name, out var version))
            {
                continue;
            }

            var candidate = leaf is null
                ? Path.Combine(directory.FullName, workerFileName)
                : Path.Combine(directory.FullName, leaf, workerFileName);
            if (File.Exists(candidate))
            {
                yield return (version, candidate);
            }
        }
    }

    /// <summary>
    /// The <c>dotnet</c> host to relaunch with: <c>DOTNET_HOST_PATH</c> (set by the SDK for the
    /// processes it starts, including MSBuild's <c>Exec</c>), then the current process when it is the
    /// host, then <c>DOTNET_ROOT</c>, then <c>dotnet</c> from <c>PATH</c>.
    /// </summary>
    internal static string ResolveMuxerPath()
    {
        var hostPath = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (!string.IsNullOrEmpty(hostPath) && File.Exists(hostPath))
        {
            return hostPath;
        }

        var processPath = Environment.ProcessPath;
        if (processPath is not null
            && string.Equals(Path.GetFileNameWithoutExtension(processPath), "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            return processPath;
        }

        var muxerFileName = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";
        var dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        if (!string.IsNullOrEmpty(dotnetRoot) && File.Exists(Path.Combine(dotnetRoot, muxerFileName)))
        {
            return Path.Combine(dotnetRoot, muxerFileName);
        }

        return "dotnet";
    }

    internal static ProcessStartInfo CreateStartInfo(string muxerPath, string depsPath, string runtimeConfigPath, string workerPath, IEnumerable<string> args)
    {
        var startInfo = new ProcessStartInfo(muxerPath) { UseShellExecute = false };
        startInfo.ArgumentList.Add("exec");
        startInfo.ArgumentList.Add("--depsfile");
        startInfo.ArgumentList.Add(depsPath);
        startInfo.ArgumentList.Add("--runtimeconfig");
        startInfo.ArgumentList.Add(runtimeConfigPath);
        startInfo.ArgumentList.Add(workerPath);
        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        startInfo.Environment[WorkerEnvironmentVariable] = "1";
        return startInfo;
    }
}
