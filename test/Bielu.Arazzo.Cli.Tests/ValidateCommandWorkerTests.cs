// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bielu.Arazzo.Cli.Commands;
using Shouldly;
using Xunit;

namespace Bielu.Arazzo.Cli.Tests;

public class ValidateCommandWorkerTests : IAsyncLifetime
{
    private readonly string _tempDir;
    private readonly string _validDocPath;
    private readonly string _invalidDocPath;

    public ValidateCommandWorkerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arazzo_cli_validate_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _validDocPath = Path.Combine(_tempDir, "valid.json");
        _invalidDocPath = Path.Combine(_tempDir, "invalid.json");
    }

    public Task InitializeAsync()
    {
        File.WriteAllText(_validDocPath, """
        {
            "arazzo": "1.1.0",
            "info": { "title": "Valid workflow", "version": "1.0.0" },
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

        File.WriteAllText(_invalidDocPath, """
        {
            "arazzo": "1.1.0",
            "info": { "title": "Invalid workflow", "version": "1.0.0" },
            "sourceDescriptions": [
                { "name": "api", "url": "https://example.com/openapi.json", "type": "openapi" }
            ],
            "workflows": [
                {
                    "workflowId": "dupe",
                    "steps": [
                        { "stepId": "step1", "operationId": "$sourceDescriptions.api.op1", "operationPath": "/x" }
                    ]
                },
                {
                    "workflowId": "dupe",
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

    private static ValidateCommandContext Context(string file)
    {
        var context = new ValidateCommandContext();
        context.Files.Add(file);
        return context;
    }

    private static int Run(ValidateCommandContext context) =>
        new ValidateCommandWorker(context, _ => { }, _ => { }, _ => { }).Process();

    [Fact]
    public void Process_WithValidFile_ReturnsZero()
    {
        // Arrange
        var context = Context(_validDocPath);

        // Act
        var result = Run(context);

        // Assert
        result.ShouldBe(0);
    }

    [Fact]
    public void Process_WithDuplicateWorkflowIdAndMutuallyExclusiveTargets_ReturnsOne()
    {
        // Arrange
        var context = Context(_invalidDocPath);

        // Act
        var result = Run(context);

        // Assert
        result.ShouldBe(1);
    }

    [Fact]
    public void Process_WithMissingFile_ReturnsOne()
    {
        // Arrange
        var context = Context(Path.Combine(_tempDir, "missing.json"));

        // Act
        var result = Run(context);

        // Assert
        result.ShouldBe(1);
    }

    [Fact]
    public void Process_Glob_MatchingOnlyValidDocuments_ReturnsZero()
    {
        // Arrange
        // Asserting 0 is what makes this meaningful: a glob that expanded to nothing also returns 1,
        // so only a success proves the expansion actually found and validated files.
        var context = Context(Path.Combine(_tempDir, "valid*.json"));

        // Act
        var result = Run(context);

        // Assert
        result.ShouldBe(0);
    }

    [Fact]
    public void Process_Glob_IncludingAnInvalidDocument_ReturnsOne()
    {
        // Arrange
        var context = Context(Path.Combine(_tempDir, "*.json"));

        // Act
        var result = Run(context);

        // Assert
        result.ShouldBe(1);
    }

    [Fact]
    public void Process_Glob_MatchingNothing_ReturnsOne()
    {
        // Arrange
        var context = Context(Path.Combine(_tempDir, "nothing-here-*.json"));

        // Act
        var result = Run(context);

        // Assert
        result.ShouldBe(1);
    }
}
