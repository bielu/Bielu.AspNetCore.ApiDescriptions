// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bielu.Arazzo.Cli.Commands;
using Shouldly;
using Xunit;

namespace Bielu.Arazzo.Cli.Tests;

public class DiffCommandWorkerTests : IAsyncLifetime
{
    private readonly string _tempDir;
    private readonly string _basePath;
    private readonly string _headPath;

    public DiffCommandWorkerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arazzo_cli_diff_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _basePath = Path.Combine(_tempDir, "base.json");
        _headPath = Path.Combine(_tempDir, "head.json");
    }

    public Task InitializeAsync()
    {
        File.WriteAllText(_basePath, """
        {
            "arazzo": "1.1.0",
            "info": { "title": "Workflow", "version": "1.0.0" },
            "sourceDescriptions": [
                { "name": "api", "url": "https://example.com/openapi.json", "type": "openapi" }
            ],
            "workflows": [
                {
                    "workflowId": "doThing",
                    "steps": [
                        { "stepId": "step1", "operationId": "$sourceDescriptions.api.op1" },
                        { "stepId": "step2", "operationId": "$sourceDescriptions.api.op2" }
                    ]
                }
            ]
        }
        """);

        File.WriteAllText(_headPath, """
        {
            "arazzo": "1.1.0",
            "info": { "title": "Workflow", "version": "1.1.0" },
            "sourceDescriptions": [
                { "name": "api", "url": "https://example.com/openapi.json", "type": "openapi" }
            ],
            "workflows": [
                {
                    "workflowId": "doThing",
                    "steps": [
                        { "stepId": "step1", "operationId": "$sourceDescriptions.api.op1" }
                    ]
                }
            ]
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

    private static int Run(DiffCommandContext context) =>
        new DiffCommandWorker(context, _ => { }, _ => { }, _ => { }).Process();

    [Fact]
    public void Process_WithRemovedStep_ReturnsZeroWithoutFailOnBreaking()
    {
        // Arrange
        var context = new DiffCommandContext { BasePath = _basePath, HeadPath = _headPath };

        // Act
        var result = Run(context);

        // Assert
        // Breaking changes are reported, but only --fail-on-breaking turns them into a failing exit code.
        result.ShouldBe(0);
    }

    [Fact]
    public void Process_WithRemovedStepAndFailOnBreaking_ReturnsOne()
    {
        // Arrange
        var context = new DiffCommandContext { BasePath = _basePath, HeadPath = _headPath, FailOnBreaking = true };

        // Act
        var result = Run(context);

        // Assert
        result.ShouldBe(1);
    }

    [Fact]
    public void Process_WithMissingBaseFile_ReturnsOne()
    {
        // Arrange
        var context = new DiffCommandContext
        {
            BasePath = Path.Combine(_tempDir, "missing.json"),
            HeadPath = _headPath,
        };

        // Act
        var result = Run(context);

        // Assert
        result.ShouldBe(1);
    }

    [Fact]
    public void Process_WithIdenticalDocuments_ReturnsZero()
    {
        // Arrange
        var context = new DiffCommandContext { BasePath = _basePath, HeadPath = _basePath, FailOnBreaking = true };

        // Act
        var result = Run(context);

        // Assert
        // No changes at all, so --fail-on-breaking has nothing to trip on.
        result.ShouldBe(0);
    }
}
