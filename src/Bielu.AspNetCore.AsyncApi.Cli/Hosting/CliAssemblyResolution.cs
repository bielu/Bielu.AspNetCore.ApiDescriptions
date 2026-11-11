// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace Bielu.AspNetCore.AsyncApi.Cli.Hosting;

/// <summary>
/// Lets the CLI find its own dependencies when <see cref="TargetRuntimeLauncher"/> relaunches it under
/// the target application's runtime configuration.
/// </summary>
/// <remarks>
/// A relaunched worker runs with the application's <c>deps.json</c>, so the trusted platform assemblies
/// are the application's: its own copies of <c>Bielu.AspNetCore.AsyncApi</c>, ByteBard and so on, which
/// keeps types identical to the ones in the application's container. Assemblies only the CLI uses
/// (<c>Bielu.Cli.Shared</c>, the merger) are not in that list, so they are loaded from the CLI's
/// directory here. This is a module initializer rather than a statement in <c>Main</c>, because
/// compiling <c>Main</c> already needs <c>Bielu.Cli.Shared</c>. When the CLI runs normally, its own
/// <c>deps.json</c> lists everything and this never triggers.
/// </remarks>
internal static class CliAssemblyResolution
{
    [ModuleInitializer]
    internal static void Register()
    {
        var directory = Path.GetDirectoryName(typeof(CliAssemblyResolution).Assembly.Location);
        if (string.IsNullOrEmpty(directory))
        {
            return;
        }

        AssemblyLoadContext.Default.Resolving += (context, assemblyName) => Resolve(context, assemblyName, directory);
    }

    internal static Assembly? Resolve(AssemblyLoadContext context, AssemblyName assemblyName, string directory)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(assemblyName);
        ArgumentNullException.ThrowIfNull(directory);

        if (string.IsNullOrEmpty(assemblyName.Name))
        {
            return null;
        }

        var candidate = Path.Combine(directory, assemblyName.Name + ".dll");
        return File.Exists(candidate) ? context.LoadFromAssemblyPath(candidate) : null;
    }
}
