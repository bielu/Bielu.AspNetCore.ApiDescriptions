// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Nodes;
using Bielu.Overlay.Cli.Commands;
using Shouldly;
using Xunit;

namespace Bielu.Overlay.Cli.Tests;

public class ApplyCommandWorkerTests : IAsyncLifetime
{
    private readonly string _tempDir;
    private readonly string _asyncApiPath;
    private readonly string _yamlDocPath;
    private readonly string _overlayPath;
    private readonly string _secondOverlayPath;
    private readonly string _missTargetOverlayPath;

    public ApplyCommandWorkerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"overlay_cli_apply_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _asyncApiPath = Path.Combine(_tempDir, "asyncapi.json");
        _yamlDocPath = Path.Combine(_tempDir, "asyncapi.yaml");
        _overlayPath = Path.Combine(_tempDir, "first.overlay.yaml");
        _secondOverlayPath = Path.Combine(_tempDir, "second.overlay.yaml");
        _missTargetOverlayPath = Path.Combine(_tempDir, "miss.overlay.yaml");
    }

    public Task InitializeAsync()
    {
        File.WriteAllText(_asyncApiPath, """
        {
          "asyncapi": "3.0.0",
          "info": { "title": "Streetlights", "version": "1.0.0" },
          "channels": {
            "lightMeasured": { "address": "light/measured" },
            "internalDebug": { "address": "internal/debug" }
          }
        }
        """);

        File.WriteAllText(_yamlDocPath, """
        asyncapi: 3.0.0
        info:
          title: Streetlights
          version: 1.0.0
        channels:
          lightMeasured:
            address: light/measured
        """);

        File.WriteAllText(_overlayPath, """
        overlay: 1.1.0
        info: { title: Strip internal, version: 1.0.0 }
        actions:
          - target: $.channels.internalDebug
            remove: true
        """);

        File.WriteAllText(_secondOverlayPath, """
        overlay: 1.1.0
        info: { title: Add description, version: 1.0.0 }
        actions:
          - target: $.info
            update:
              description: Public distribution
        """);

        File.WriteAllText(_missTargetOverlayPath, """
        overlay: 1.1.0
        info: { title: Targets nothing, version: 1.0.0 }
        actions:
          - target: $.channels.doesNotExist
            remove: true
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

    private ApplyCommandContext Context(string documentPath, string outputPath, params string[] overlays)
    {
        var context = new ApplyCommandContext { FilePath = documentPath, OutputPath = outputPath };
        foreach (var overlay in overlays)
        {
            context.Overlays.Add(overlay);
        }

        return context;
    }

    private static int Run(ApplyCommandContext context) =>
        new ApplyCommandWorker(context, _ => { }, _ => { }, _ => { }).Process();

    [Fact]
    public void Process_AppliesOverlayAndWritesOutput()
    {
        // Arrange
        var output = Path.Combine(_tempDir, "out.json");
        var context = Context(_asyncApiPath, output, _overlayPath);

        // Act
        var exitCode = Run(context);

        // Assert
        exitCode.ShouldBe(0);
        var result = JsonNode.Parse(File.ReadAllText(output))!;
        result["channels"]!.AsObject().ContainsKey("internalDebug").ShouldBeFalse();
        result["channels"]!.AsObject().ContainsKey("lightMeasured").ShouldBeTrue();
    }

    [Fact]
    public void Process_AppliesMultipleOverlaysInOrder()
    {
        // Arrange
        var output = Path.Combine(_tempDir, "out.json");
        var context = Context(_asyncApiPath, output, _overlayPath, _secondOverlayPath);

        // Act
        var exitCode = Run(context);

        // Assert
        exitCode.ShouldBe(0);
        var result = JsonNode.Parse(File.ReadAllText(output))!;
        result["channels"]!.AsObject().ContainsKey("internalDebug").ShouldBeFalse();
        result["info"]!["description"]!.GetValue<string>().ShouldBe("Public distribution");
    }

    [Fact]
    public void Process_LeavesTheSourceDocumentOnDiskUntouched()
    {
        // Arrange
        var before = File.ReadAllText(_asyncApiPath);
        var context = Context(_asyncApiPath, Path.Combine(_tempDir, "out.json"), _overlayPath);

        // Act
        var exitCode = Run(context);

        // Assert
        exitCode.ShouldBe(0);
        File.ReadAllText(_asyncApiPath).ShouldBe(before);
    }

    [Fact]
    public void Process_InfersYamlOutputFromTheOutputExtension()
    {
        // Arrange
        var output = Path.Combine(_tempDir, "out.yaml");
        var context = Context(_asyncApiPath, output, _overlayPath);

        // Act
        var exitCode = Run(context);

        // Assert
        exitCode.ShouldBe(0);
        var text = File.ReadAllText(output);
        text.ShouldContain("asyncapi: 3.0.0");
        text.ShouldNotStartWith("{");
    }

    [Fact]
    public void Process_ExplicitFormatBeatsTheOutputExtension()
    {
        // Arrange
        var output = Path.Combine(_tempDir, "out.yaml");
        var context = Context(_asyncApiPath, output, _overlayPath);
        context.Format = "json";

        // Act
        var exitCode = Run(context);

        // Assert
        exitCode.ShouldBe(0);
        File.ReadAllText(output).TrimStart().ShouldStartWith("{");
    }

    [Fact]
    public void Process_ReadsAYamlSourceDocument()
    {
        // Arrange
        var output = Path.Combine(_tempDir, "out.json");
        var context = Context(_yamlDocPath, output, _secondOverlayPath);

        // Act
        var exitCode = Run(context);

        // Assert
        exitCode.ShouldBe(0);
        var result = JsonNode.Parse(File.ReadAllText(output))!;
        result["info"]!["description"]!.GetValue<string>().ShouldBe("Public distribution");
        result["channels"]!["lightMeasured"]!["address"]!.GetValue<string>().ShouldBe("light/measured");
    }

    [Fact]
    public void Process_MissingDocument_ReturnsOne()
    {
        // Arrange
        var context = Context(Path.Combine(_tempDir, "nope.json"), Path.Combine(_tempDir, "out.json"), _overlayPath);

        // Act
        var exitCode = Run(context);

        // Assert
        exitCode.ShouldBe(1);
    }

    [Fact]
    public void Process_MissingOverlay_ReturnsOne()
    {
        // Arrange
        var context = Context(_asyncApiPath, Path.Combine(_tempDir, "out.json"), Path.Combine(_tempDir, "nope.yaml"));

        // Act
        var exitCode = Run(context);

        // Assert
        exitCode.ShouldBe(1);
    }

    [Fact]
    public void Process_ZeroMatchTarget_PassesByDefault_AndFailsUnderStrict()
    {
        // Arrange
        var lenient = Context(_asyncApiPath, Path.Combine(_tempDir, "lenient.json"), _missTargetOverlayPath);
        var strict = Context(_asyncApiPath, Path.Combine(_tempDir, "strict.json"), _missTargetOverlayPath);
        strict.Strict = true;

        // Act
        var lenientResult = Run(lenient);
        var strictResult = Run(strict);

        // Assert
        lenientResult.ShouldBe(0);
        strictResult.ShouldBe(1);
    }

    [Fact]
    public void Process_WhenApplicationFails_NoOutputIsWritten()
    {
        // Arrange
        var output = Path.Combine(_tempDir, "not-written.json");
        var context = Context(_asyncApiPath, output, _missTargetOverlayPath);
        context.Strict = true;

        // Act
        var exitCode = Run(context);

        // Assert
        exitCode.ShouldBe(1);
        File.Exists(output).ShouldBeFalse("a failed run must not leave a half-transformed document behind");
    }
}
