// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bielu.Arazzo.Cli.Commands;
using Shouldly;
using Xunit;

namespace Bielu.Arazzo.Cli.Tests;

public class LintCommandWorkerTests : IAsyncLifetime
{
    private readonly string _tempDir;
    private readonly string _cleanDocPath;
    private readonly string _cyclicDocPath;

    public LintCommandWorkerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arazzo_cli_lint_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _cleanDocPath = Path.Combine(_tempDir, "clean.json");
        _cyclicDocPath = Path.Combine(_tempDir, "cyclic.json");
    }

    public Task InitializeAsync()
    {
        File.WriteAllText(_cleanDocPath, """
        {
            "arazzo": "1.1.0",
            "info": { "title": "Clean workflow", "summary": "A workflow", "version": "1.0.0" },
            "sourceDescriptions": [
                { "name": "api", "url": "https://example.com/openapi.json", "type": "openapi" }
            ],
            "workflows": [
                {
                    "workflowId": "doThing",
                    "summary": "Does a thing",
                    "steps": [
                        { "stepId": "step1", "description": "first step", "operationId": "$sourceDescriptions.api.op1" }
                    ]
                }
            ]
        }
        """);

        File.WriteAllText(_cyclicDocPath, """
        {
            "arazzo": "1.1.0",
            "info": { "title": "Cyclic workflow", "version": "1.0.0" },
            "sourceDescriptions": [
                { "name": "api", "url": "https://example.com/openapi.json", "type": "openapi" }
            ],
            "workflows": [
                {
                    "workflowId": "cyclic",
                    "steps": [
                        { "stepId": "a", "operationId": "$sourceDescriptions.api.op1", "dependsOn": ["b"] },
                        { "stepId": "b", "operationId": "$sourceDescriptions.api.op2", "dependsOn": ["a"] }
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
    public void Process_WithCleanDocument_ReturnsZero()
    {
        var context = new LintCommandContext();
        context.Files.Add(_cleanDocPath);

        var worker = new LintCommandWorker(context, _ => { }, _ => { }, _ => { });

        worker.Process().ShouldBe(0);
    }

    [Fact]
    public void Process_WithDependsOnCycle_ReturnsOne()
    {
        var context = new LintCommandContext();
        context.Files.Add(_cyclicDocPath);

        var worker = new LintCommandWorker(context, _ => { }, _ => { }, _ => { });

        worker.Process().ShouldBe(1);
    }

    [Fact]
    public void Process_WithCleanDocumentInStrictMode_StillReturnsZero()
    {
        var context = new LintCommandContext { Strict = true };
        context.Files.Add(_cleanDocPath);

        var worker = new LintCommandWorker(context, _ => { }, _ => { }, _ => { });

        worker.Process().ShouldBe(0);
    }
}
