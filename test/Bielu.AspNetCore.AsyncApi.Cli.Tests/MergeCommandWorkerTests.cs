// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Bielu.AspNetCore.AsyncApi.Cli.Commands;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Cli.Tests;

/// <summary>
/// Unit and integration tests for MergeCommandWorker.
/// </summary>
public class MergeCommandWorkerTests : IAsyncLifetime
{
    private readonly string _tempDir;
    private readonly string _doc1Path;
    private readonly string _doc2Path;

    public MergeCommandWorkerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"asyncapi_cli_merge_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _doc1Path = Path.Combine(_tempDir, "service_a.json");
        _doc2Path = Path.Combine(_tempDir, "service_b.json");
    }

    public Task InitializeAsync()
    {
        File.WriteAllText(_doc1Path, """
        {
            "asyncapi": "3.0.0",
            "info": { "title": "Service A", "version": "1.0.0" },
            "channels": {
                "userChannel": {
                    "address": "user/signedup",
                    "description": "User signup events"
                }
            }
        }
        """);

        File.WriteAllText(_doc2Path, """
        {
            "asyncapi": "3.0.0",
            "info": { "title": "Service B", "version": "2.0.0" },
            "channels": {
                "orderChannel": {
                    "address": "order/placed",
                    "description": "Order placed events"
                }
            }
        }
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

    [Fact]
    public void Constructor_ThrowsForNullContext()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new MergeCommandWorker(null!, _ => { }, _ => { }));
    }

    [Fact]
    public void Process_WithValidSources_ReturnsZeroExitCode()
    {
        // Arrange
        var outputPath = Path.Combine(_tempDir, "merged.json");
        var context = new MergeCommandContext { OutputPath = outputPath };
        context.Sources.Add(_doc1Path);
        context.Sources.Add(_doc2Path);

        var worker = new MergeCommandWorker(
            context,
            writeInfo: _ => { },
            writeError: _ => { });

        // Act
        var result = worker.Process();

        // Assert
        result.ShouldBe(0);
        File.Exists(outputPath).ShouldBeTrue();
    }

    [Fact]
    public void Process_ProducesValidJsonOutput()
    {
        // Arrange
        var outputPath = Path.Combine(_tempDir, "merged_valid.json");
        var context = new MergeCommandContext { OutputPath = outputPath };
        context.Sources.Add(_doc1Path);
        context.Sources.Add(_doc2Path);

        var worker = new MergeCommandWorker(
            context,
            writeInfo: _ => { },
            writeError: _ => { });

        // Act
        worker.Process();

        // Assert
        var content = File.ReadAllText(outputPath);
        var action = () => JsonDocument.Parse(content);
        action.ShouldNotThrow();
    }

    [Fact]
    public void Process_MergedDocumentContainsAllChannels()
    {
        // Arrange
        var outputPath = Path.Combine(_tempDir, "merged_channels.json");
        var context = new MergeCommandContext { OutputPath = outputPath };
        context.Sources.Add(_doc1Path);
        context.Sources.Add(_doc2Path);

        var worker = new MergeCommandWorker(
            context,
            writeInfo: _ => { },
            writeError: _ => { });

        // Act
        worker.Process();

        // Assert
        var content = File.ReadAllText(outputPath);
        content.ShouldContain("userChannel");
        content.ShouldContain("orderChannel");
    }

    [Fact]
    public void Process_WithPrefixes_PrefixesKeys()
    {
        // Arrange
        var outputPath = Path.Combine(_tempDir, "merged_prefixed.json");
        var context = new MergeCommandContext { OutputPath = outputPath };
        context.Sources.Add(_doc1Path);
        context.Sources.Add(_doc2Path);
        context.Prefixes.Add("svcA");
        context.Prefixes.Add("svcB");

        var worker = new MergeCommandWorker(
            context,
            writeInfo: _ => { },
            writeError: _ => { });

        // Act
        worker.Process();

        // Assert
        var content = File.ReadAllText(outputPath);
        content.ShouldContain("svcA_userChannel");
        content.ShouldContain("svcB_orderChannel");
    }

    [Fact]
    public void Process_WithCustomTitleAndVersion_UsesCustomInfo()
    {
        // Arrange
        var outputPath = Path.Combine(_tempDir, "merged_info.json");
        var context = new MergeCommandContext
        {
            OutputPath = outputPath,
            Title = "Platform API",
            Version = "3.0.0"
        };
        context.Sources.Add(_doc1Path);

        var worker = new MergeCommandWorker(
            context,
            writeInfo: _ => { },
            writeError: _ => { });

        // Act
        worker.Process();

        // Assert
        var content = File.ReadAllText(outputPath);
        content.ShouldContain("Platform API");
        content.ShouldContain("3.0.0");
    }

    [Fact]
    public void Process_WithNonExistentSource_ReturnsNonZeroExitCode()
    {
        // Arrange
        var outputPath = Path.Combine(_tempDir, "merged_error.json");
        var context = new MergeCommandContext { OutputPath = outputPath };
        context.Sources.Add("/tmp/nonexistent_asyncapi_12345.json");

        var errors = new List<string>();
        var worker = new MergeCommandWorker(
            context,
            writeInfo: _ => { },
            writeError: msg => errors.Add(msg));

        // Act
        var result = worker.Process();

        // Assert
        result.ShouldBe(1);
        errors.ShouldNotBeEmpty();
    }

    [Fact]
    public void Process_YamlOutput_ProducesYamlFile()
    {
        // Arrange
        var outputPath = Path.Combine(_tempDir, "merged.yaml");
        var context = new MergeCommandContext { OutputPath = outputPath };
        context.Sources.Add(_doc1Path);

        var worker = new MergeCommandWorker(
            context,
            writeInfo: _ => { },
            writeError: _ => { });

        // Act
        var result = worker.Process();

        // Assert
        result.ShouldBe(0);
        File.Exists(outputPath).ShouldBeTrue();
        var content = File.ReadAllText(outputPath);
        content.ShouldContain("asyncapi");
        // YAML should not start with { (it's not JSON)
        content.TrimStart().ShouldNotStartWith("{");
    }

    [Fact]
    public void Process_CreatesOutputDirectoryIfNotExists()
    {
        // Arrange
        var subDir = Path.Combine(_tempDir, "nested", "output");
        var outputPath = Path.Combine(subDir, "merged.json");
        var context = new MergeCommandContext { OutputPath = outputPath };
        context.Sources.Add(_doc1Path);

        var worker = new MergeCommandWorker(
            context,
            writeInfo: _ => { },
            writeError: _ => { });

        // Act
        var result = worker.Process();

        // Assert
        result.ShouldBe(0);
        File.Exists(outputPath).ShouldBeTrue();
    }

    [Fact]
    public void Process_WritesInfoMessages()
    {
        // Arrange
        var outputPath = Path.Combine(_tempDir, "merged_info_msgs.json");
        var context = new MergeCommandContext { OutputPath = outputPath };
        context.Sources.Add(_doc1Path);

        var infoMessages = new List<string>();
        var worker = new MergeCommandWorker(
            context,
            writeInfo: msg => infoMessages.Add(msg),
            writeError: _ => { });

        // Act
        worker.Process();

        // Assert
        infoMessages.ShouldNotBeEmpty();
        infoMessages.ShouldContain(m => m.Contains("Merging"));
        infoMessages.ShouldContain(m => m.Contains("written"));
    }
}
