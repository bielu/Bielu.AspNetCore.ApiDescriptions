using System.Runtime.Loader;
using Bielu.AspNetCore.AsyncApi.Cli.Commands;
using Bielu.AspNetCore.AsyncApi.Cli.Hosting;
using Bielu.Cli.Shared;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Cli.Tests;

/// <summary>
/// Unit tests for <see cref="TargetRuntimeLauncher"/>. The end-to-end relaunch (a net10.0 and a net11.0
/// application, generated from either CLI build) runs in scripts/verify-cli-runtimes.sh.
/// </summary>
public sealed class TargetRuntimeLauncherTests : IDisposable
{
    private const string WorkerFileName = "Bielu.AspNetCore.AsyncApi.Cli.dll";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "asyncapi-cli-launcher-" + Guid.NewGuid().ToString("N"));

    public TargetRuntimeLauncherTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string CreateWorker(params string[] segments)
    {
        var directory = Path.Combine([_root, .. segments]);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, WorkerFileName);
        File.WriteAllText(path, string.Empty);
        return path;
    }

    private string WriteRuntimeConfig(string json)
    {
        var path = Path.Combine(_root, "App.runtimeconfig.json");
        File.WriteAllText(path, json);
        return path;
    }

    [Theory]
    [InlineData("net10.0", 10, 0)]
    [InlineData("net11.0", 11, 0)]
    [InlineData("NET12.0", 12, 0)]
    public void TryParseTargetFramework_NetMoniker_ReturnsMajorAndMinor(string moniker, int major, int minor)
    {
        // Act
        var parsed = TargetRuntimeLauncher.TryParseTargetFramework(moniker, out var version);

        // Assert
        parsed.ShouldBeTrue();
        version.ShouldBe(new Version(major, minor));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("netstandard2.0")]
    [InlineData("net48")]
    [InlineData("net4.8")]
    [InlineData("netcoreapp3.1")]
    [InlineData("any")]
    public void TryParseTargetFramework_NotAModernNetMoniker_ReturnsFalse(string? moniker)
    {
        // Act & Assert
        TargetRuntimeLauncher.TryParseTargetFramework(moniker, out _).ShouldBeFalse();
    }

    [Fact]
    public void TryReadTargetFramework_RuntimeConfigWithTfm_ReturnsIt()
    {
        // Arrange
        var path = WriteRuntimeConfig("""{ "runtimeOptions": { "tfm": "net11.0", "framework": { "name": "Microsoft.AspNetCore.App", "version": "11.0.0" } } }""");

        // Act
        var read = TargetRuntimeLauncher.TryReadTargetFramework(path, out var version);

        // Assert
        read.ShouldBeTrue();
        version.ShouldBe(new Version(11, 0));
    }

    [Theory]
    [InlineData("""{ "runtimeOptions": { } }""")]
    [InlineData("""{ "runtimeOptions": { "tfm": 11 } }""")]
    [InlineData("""{ }""")]
    [InlineData("not json")]
    public void TryReadTargetFramework_NoUsableTfm_ReturnsFalse(string json)
    {
        // Arrange
        var path = WriteRuntimeConfig(json);

        // Act & Assert
        TargetRuntimeLauncher.TryReadTargetFramework(path, out _).ShouldBeFalse();
    }

    [Fact]
    public void TryReadTargetFramework_MissingFile_ReturnsFalse()
    {
        // Act & Assert
        TargetRuntimeLauncher.TryReadTargetFramework(Path.Combine(_root, "missing.json"), out _).ShouldBeFalse();
    }

    [Theory]
    [InlineData(10, "net10.0")]
    [InlineData(11, "net11.0")]
    [InlineData(12, "net11.0")]
    public void FindWorkerAssembly_ToolPackageLayout_PicksHighestNotAboveTarget(int targetMajor, string expectedFramework)
    {
        // Arrange: tools/<tfm>/any/, as `dotnet tool install` lays a multi-target tool out.
        var net10 = CreateWorker("tools", "net10.0", "any");
        var net11 = CreateWorker("tools", "net11.0", "any");
        var running = Path.GetDirectoryName(net11)!;

        // Act
        var worker = TargetRuntimeLauncher.FindWorkerAssembly(running, WorkerFileName, new Version(targetMajor, 0));

        // Assert
        worker.ShouldBe(expectedFramework == "net10.0" ? net10 : net11);
    }

    [Fact]
    public void FindWorkerAssembly_BuildOutputLayout_FindsSiblingFramework()
    {
        // Arrange: bin/Release/<tfm>/, running the CLI straight from its build output.
        var net10 = CreateWorker("bin", "Release", "net10.0");
        var net11 = CreateWorker("bin", "Release", "net11.0");

        // Act
        var worker = TargetRuntimeLauncher.FindWorkerAssembly(Path.GetDirectoryName(net11)!, WorkerFileName, new Version(10, 0));

        // Assert
        worker.ShouldBe(net10);
    }

    [Fact]
    public void FindWorkerAssembly_TargetOlderThanEveryBuild_ReturnsNull()
    {
        // Arrange
        var net10 = CreateWorker("tools", "net10.0", "any");

        // Act
        var worker = TargetRuntimeLauncher.FindWorkerAssembly(Path.GetDirectoryName(net10)!, WorkerFileName, new Version(9, 0));

        // Assert
        worker.ShouldBeNull();
    }

    [Fact]
    public void FindWorkerAssembly_IgnoresFrameworkFoldersWithoutTheWorker()
    {
        // Arrange
        var net10 = CreateWorker("tools", "net10.0", "any");
        Directory.CreateDirectory(Path.Combine(_root, "tools", "net11.0", "any"));
        Directory.CreateDirectory(Path.Combine(_root, "tools", "netstandard2.0", "any"));

        // Act
        var available = TargetRuntimeLauncher.FindAvailableTargetFrameworks(Path.GetDirectoryName(net10)!, WorkerFileName);

        // Assert
        available.ShouldBe([new Version(10, 0)]);
    }

    [Fact]
    public void CreateStartInfo_PassesAppRuntimeFilesWorkerAndOriginalArguments()
    {
        // Act
        var startInfo = TargetRuntimeLauncher.CreateStartInfo(
            "dotnet", "app.deps.json", "app.runtimeconfig.json", "worker.dll", ["getdocument", "--assembly", "My App"]);

        // Assert
        startInfo.FileName.ShouldBe("dotnet");
        startInfo.UseShellExecute.ShouldBeFalse();
        startInfo.ArgumentList.ShouldBe(
            ["exec", "--depsfile", "app.deps.json", "--runtimeconfig", "app.runtimeconfig.json", "worker.dll", "getdocument", "--assembly", "My App"]);
        startInfo.Environment[TargetRuntimeLauncher.WorkerEnvironmentVariable].ShouldBe("1");
    }

    [Fact]
    public void TryRelaunch_NoAssemblyPath_RunsInProcess()
    {
        // Arrange
        var context = new GetDocumentCommandContext { AssemblyName = "App" };

        // Act
        var handled = TargetRuntimeLauncher.TryRelaunch(context, [], Substitute.For<ICliLogger>(), out _);

        // Assert
        handled.ShouldBeFalse();
    }

    [Fact]
    public void TryRelaunch_NoRuntimeConfig_RunsInProcess()
    {
        // Arrange
        var assemblyPath = Path.Combine(_root, "App.dll");
        File.WriteAllText(assemblyPath, string.Empty);
        var context = new GetDocumentCommandContext { AssemblyName = "App", AssemblyPath = assemblyPath };

        // Act
        var handled = TargetRuntimeLauncher.TryRelaunch(context, [], Substitute.For<ICliLogger>(), out _);

        // Assert
        handled.ShouldBeFalse();
    }

    [Fact]
    public void TryRelaunch_AppOnTheCurrentRuntime_RunsInProcess()
    {
        // Arrange: same runtime as the test host, so existing in-process behaviour is kept.
        var assemblyPath = Path.Combine(_root, "App.dll");
        File.WriteAllText(assemblyPath, string.Empty);
        File.WriteAllText(Path.Combine(_root, "App.deps.json"), "{}");
        WriteRuntimeConfig($$"""{ "runtimeOptions": { "tfm": "net{{Environment.Version.Major}}.{{Environment.Version.Minor}}" } }""");
        var context = new GetDocumentCommandContext { AssemblyName = "App", AssemblyPath = assemblyPath };

        // Act
        var handled = TargetRuntimeLauncher.TryRelaunch(context, [], Substitute.For<ICliLogger>(), out _);

        // Assert
        handled.ShouldBeFalse();
    }

    [Fact]
    public void TryRelaunch_AppTargetsUnsupportedFramework_FailsWithAvailableBuilds()
    {
        // Arrange: net5.0 is below every build this CLI ships.
        var assemblyPath = Path.Combine(_root, "App.dll");
        File.WriteAllText(assemblyPath, string.Empty);
        File.WriteAllText(Path.Combine(_root, "App.deps.json"), "{}");
        WriteRuntimeConfig("""{ "runtimeOptions": { "tfm": "net5.0" } }""");
        var context = new GetDocumentCommandContext { AssemblyName = "App", AssemblyPath = assemblyPath };
        var logger = Substitute.For<ICliLogger>();

        // Act
        var handled = TargetRuntimeLauncher.TryRelaunch(context, [], logger, out var exitCode);

        // Assert
        handled.ShouldBeTrue();
        exitCode.ShouldBe(CliExitCode.Failure);
        logger.Received(1).Error(Arg.Is<string>(message => message.Contains("net5.0") && message.Contains("does not support")));
    }

    [Fact]
    public void TryRelaunch_ThrowsForNullArguments()
    {
        // Arrange
        var context = new GetDocumentCommandContext();
        var logger = Substitute.For<ICliLogger>();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => TargetRuntimeLauncher.TryRelaunch(null!, [], logger, out _));
        Should.Throw<ArgumentNullException>(() => TargetRuntimeLauncher.TryRelaunch(context, null!, logger, out _));
        Should.Throw<ArgumentNullException>(() => TargetRuntimeLauncher.TryRelaunch(context, [], null!, out _));
    }

    [Fact]
    public void ResolveMuxerPath_ReturnsAHost()
    {
        // Act
        var muxer = TargetRuntimeLauncher.ResolveMuxerPath();

        // Assert
        Path.GetFileNameWithoutExtension(muxer).ShouldBe("dotnet");
    }

    [Fact]
    public void Resolve_AssemblyInDirectory_LoadsIt()
    {
        // Arrange: the CLI's own Cli.Shared, resolved from the directory it was built into.
        var directory = Path.GetDirectoryName(typeof(CliExitCode).Assembly.Location)!;
        var context = new AssemblyLoadContext("resolve-test", isCollectible: true);

        try
        {
            // Act
            var assembly = CliAssemblyResolution.Resolve(context, typeof(CliExitCode).Assembly.GetName(), directory);

            // Assert
            assembly.ShouldNotBeNull();
            assembly.GetName().Name.ShouldBe("Bielu.Cli.Shared");
        }
        finally
        {
            context.Unload();
        }
    }

    [Fact]
    public void Resolve_AssemblyNotInDirectory_ReturnsNull()
    {
        // Act
        var assembly = CliAssemblyResolution.Resolve(
            AssemblyLoadContext.Default, new System.Reflection.AssemblyName("Does.Not.Exist"), _root);

        // Assert
        assembly.ShouldBeNull();
    }
}
