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

    [Fact]
    public void Process_WithValidFile_ReturnsZero()
    {
        var context = new ValidateCommandContext();
        context.Files.Add(_validDocPath);

        var worker = new ValidateCommandWorker(context, _ => { }, _ => { }, _ => { });

        worker.Process().ShouldBe(0);
    }

    [Fact]
    public void Process_WithDuplicateWorkflowIdAndMutuallyExclusiveTargets_ReturnsOne()
    {
        var context = new ValidateCommandContext();
        context.Files.Add(_invalidDocPath);

        var worker = new ValidateCommandWorker(context, _ => { }, _ => { }, _ => { });

        worker.Process().ShouldBe(1);
    }

    [Fact]
    public void Process_WithMissingFile_ReturnsOne()
    {
        var context = new ValidateCommandContext();
        context.Files.Add(Path.Combine(_tempDir, "missing.json"));

        var worker = new ValidateCommandWorker(context, _ => { }, _ => { }, _ => { });

        worker.Process().ShouldBe(1);
    }

    [Fact]
    public void Process_WithGlob_FindsBothFiles()
    {
        var context = new ValidateCommandContext();
        context.Files.Add(Path.Combine(_tempDir, "*.json"));

        var worker = new ValidateCommandWorker(context, _ => { }, _ => { }, _ => { });

        worker.Process().ShouldBe(1);
    }
}
