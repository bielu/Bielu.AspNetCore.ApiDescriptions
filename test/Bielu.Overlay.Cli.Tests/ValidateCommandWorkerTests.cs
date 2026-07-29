// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bielu.Overlay.Cli.Commands;
using Shouldly;
using Xunit;

namespace Bielu.Overlay.Cli.Tests;

public class ValidateCommandWorkerTests : IAsyncLifetime
{
    private readonly string _tempDir;
    private readonly string _validPath;
    private readonly string _invalidPath;
    private readonly string _warningOnlyPath;

    public ValidateCommandWorkerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"overlay_cli_validate_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _validPath = Path.Combine(_tempDir, "valid.yaml");
        _invalidPath = Path.Combine(_tempDir, "invalid.yaml");
        _warningOnlyPath = Path.Combine(_tempDir, "warning.yaml");
    }

    public Task InitializeAsync()
    {
        File.WriteAllText(_validPath, """
        overlay: 1.1.0
        info: { title: Valid overlay, version: 1.0.0 }
        actions:
          - target: $.info
            update:
              description: hello
        """);

        // Unparseable target, and a 1.1.0-only feature on a 1.0.0 document.
        File.WriteAllText(_invalidPath, """
        overlay: 1.0.0
        info: { title: Invalid overlay, version: 1.0.0 }
        actions:
          - target: '$.$$!!nope'
            copy: $.info
        """);

        // Legal but pointless: an action that does nothing is a warning, not an error.
        File.WriteAllText(_warningOnlyPath, """
        overlay: 1.1.0
        info: { title: No-op overlay, version: 1.0.0 }
        actions:
          - target: $.info
        """);

        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }

        return Task.CompletedTask;
    }

    private static int Run(ValidateCommandContext context) =>
        new ValidateCommandWorker(context, _ => { }, _ => { }, _ => { }).Process();

    private static ValidateCommandContext Context(params string[] files)
    {
        var context = new ValidateCommandContext();
        foreach (var file in files)
        {
            context.Files.Add(file);
        }

        return context;
    }

    [Fact]
    public void Process_ValidOverlay_ReturnsZero()
    {
        Run(Context(_validPath)).ShouldBe(0);
    }

    [Fact]
    public void Process_InvalidOverlay_ReturnsOne()
    {
        Run(Context(_invalidPath)).ShouldBe(1);
    }

    [Fact]
    public void Process_WarningOnly_PassesByDefault_AndFailsUnderStrict()
    {
        Run(Context(_warningOnlyPath)).ShouldBe(0);

        var strict = Context(_warningOnlyPath);
        strict.Strict = true;
        Run(strict).ShouldBe(1);
    }

    [Fact]
    public void Process_MissingFile_ReturnsOne()
    {
        Run(Context(Path.Combine(_tempDir, "nope.yaml"))).ShouldBe(1);
    }

    [Fact]
    public void Process_Glob_FindsEveryOverlay()
    {
        Run(Context(Path.Combine(_tempDir, "*.yaml"))).ShouldBe(1);
    }
}
