// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bielu.AspNetCore.AsyncApi.Cli.Commands;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Cli.Tests;

public class DiffCommandWorkerTests : IAsyncLifetime
{
    private readonly string _tempDir;
    private readonly string _baseDocPath;
    private readonly string _headDocPath;

    public DiffCommandWorkerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"asyncapi_cli_diff_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _baseDocPath = Path.Combine(_tempDir, "base.json");
        _headDocPath = Path.Combine(_tempDir, "head.json");
    }

    public Task InitializeAsync()
    {
        File.WriteAllText(_baseDocPath, """
        {
            "asyncapi": "3.0.0",
            "info": { "title": "Base API", "version": "1.0.0" },
            "channels": {
                "userChannel": {
                    "address": "user/signedup"
                }
            },
            "operations": {
                "onUserSignedup": {
                    "action": "receive",
                    "channel": { "$ref": "#/channels/userChannel" }
                }
            }
        }
        """);

        File.WriteAllText(_headDocPath, """
        {
            "asyncapi": "3.0.0",
            "info": { "title": "Head API", "version": "1.0.0" },
            "channels": {
                "userChannel": {
                    "address": "user/signedup/v2"
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
    public void Process_WithBreakingChanges_ReturnsZeroByDefault()
    {
        var context = new DiffCommandContext
        {
            BasePath = _baseDocPath,
            HeadPath = _headDocPath
        };

        var worker = new DiffCommandWorker(context, _ => { }, _ => { }, _ => { });

        var result = worker.Process();

        result.ShouldBe(0);
    }

    [Fact]
    public void Process_WithFailOnBreaking_ReturnsOneForBreakingChanges()
    {
        var context = new DiffCommandContext
        {
            BasePath = _baseDocPath,
            HeadPath = _headDocPath,
            FailOnBreaking = true
        };

        var worker = new DiffCommandWorker(context, _ => { }, _ => { }, _ => { });

        var result = worker.Process();

        result.ShouldBe(1);
    }
}
