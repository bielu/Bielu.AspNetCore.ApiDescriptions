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
        // Arrange
        var context = Context(_validPath);

        // Act
        var result = Run(context);

        // Assert
        result.ShouldBe(0);
    }

    [Fact]
    public void Process_InvalidOverlay_ReturnsOne()
    {
        // Arrange
        var context = Context(_invalidPath);

        // Act
        var result = Run(context);

        // Assert
        result.ShouldBe(1);
    }

    [Fact]
    public void Process_WarningOnlyLenient_ReturnsZero()
    {
        // Arrange
        var context = Context(_warningOnlyPath);

        // Act
        var result = Run(context);

        // Assert
        result.ShouldBe(0);
    }

    [Fact]
    public void Process_WarningOnlyStrict_ReturnsOne()
    {
        // Arrange
        var context = Context(_warningOnlyPath);
        context.Strict = true;

        // Act
        var result = Run(context);

        // Assert
        result.ShouldBe(1);
    }

    [Fact]
    public void Process_MissingFile_ReturnsOne()
    {
        // Arrange
        var context = Context(Path.Combine(_tempDir, "nope.yaml"));

        // Act
        var result = Run(context);

        // Assert
        result.ShouldBe(1);
    }

    [Fact]
    public void Process_Glob_MatchingOnlyValidOverlays_ReturnsZero()
    {
        // Arrange
        // Asserting 0 is what makes this meaningful: a glob that expanded to nothing also returns 1,
        // so only a success proves the expansion actually found and validated files.
        var context = Context(Path.Combine(_tempDir, "valid*.yaml"));

        // Act
        var result = Run(context);

        // Assert
        result.ShouldBe(0);
    }

    [Fact]
    public void Process_Glob_IncludingAnInvalidOverlay_ReturnsOne()
    {
        // Arrange
        var context = Context(Path.Combine(_tempDir, "*.yaml"));

        // Act
        var result = Run(context);

        // Assert
        result.ShouldBe(1);
    }

    [Fact]
    public void Process_Glob_MatchingNothing_ReturnsOne()
    {
        // Arrange
        var context = Context(Path.Combine(_tempDir, "nothing-here-*.yaml"));

        // Act
        var result = Run(context);

        // Assert
        result.ShouldBe(1);
    }
}
