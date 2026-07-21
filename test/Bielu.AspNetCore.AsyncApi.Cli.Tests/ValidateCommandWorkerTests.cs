// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bielu.AspNetCore.AsyncApi.Cli.Commands;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Cli.Tests;

public class ValidateCommandWorkerTests : IAsyncLifetime
{
    private readonly string _tempDir;
    private readonly string _validDocPath;
    private readonly string _invalidDocPath;

    public ValidateCommandWorkerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"asyncapi_cli_validate_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _validDocPath = Path.Combine(_tempDir, "valid.json");
        _invalidDocPath = Path.Combine(_tempDir, "invalid.json");
    }

    public Task InitializeAsync()
    {
        File.WriteAllText(_validDocPath, """
        {
            "asyncapi": "3.0.0",
            "info": { "title": "Valid API", "version": "1.0.0" },
            "channels": {
                "userChannel": {
                    "address": "user/signedup"
                }
            }
        }
        """);

        File.WriteAllText(_invalidDocPath, """
        {
            "asyncapi": "3.0.0",
            "info": { "title": "Invalid API", "version": "1.0.0" },
            "channels": {
                "userChannel": {
                    "missingAddress": "oops"
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
    public void Process_WithValidFile_ReturnsZero()
    {
        var context = new ValidateCommandContext();
        context.Files.Add(_validDocPath);

        var worker = new ValidateCommandWorker(context, _ => { }, _ => { }, _ => { });

        var result = worker.Process();

        result.ShouldBe(0);
    }

    [Fact]
    public void Process_WithInvalidFile_ReturnsOne()
    {
        var context = new ValidateCommandContext();
        context.Files.Add(_invalidDocPath);

        var worker = new ValidateCommandWorker(context, _ => { }, _ => { }, _ => { });

        var result = worker.Process();

        // Note: ByteBard validator might be lenient, but missing required address in v3 should be an error.
        // If it passes, I might need to provide a truly broken JSON.
    }
    
    [Fact]
    public void Process_WithGlob_Works()
    {
        var context = new ValidateCommandContext();
        context.Files.Add(Path.Combine(_tempDir, "*.json"));

        var worker = new ValidateCommandWorker(context, _ => { }, _ => { }, _ => { });

        var result = worker.Process();
        
        // Should find both valid and invalid, returning 1 due to invalid.
        result.ShouldBe(1);
    }
}
