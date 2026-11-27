// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bielu.Arazzo.Cli.Commands;
using Shouldly;
using Xunit;

namespace Bielu.Arazzo.Cli.Tests;

public class ValidateSourceReferencesTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _arazzoPath;

    public ValidateSourceReferencesTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arazzo_cli_sources_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _arazzoPath = Path.Combine(_tempDir, "workflows.arazzo.json");

        File.WriteAllText(_arazzoPath, """
        {
            "arazzo": "1.1.0",
            "info": { "title": "Checkout", "version": "1.0.0" },
            "sourceDescriptions": [
                { "name": "shop", "url": "./openapi.json", "type": "openapi" },
                { "name": "events", "url": "asyncapi.yaml", "type": "asyncapi" },
                { "name": "partner", "url": "https://partner.example.com/openapi.json", "type": "openapi" }
            ],
            "workflows": [
                {
                    "workflowId": "checkout",
                    "steps": [
                        { "stepId": "order", "operationId": "$sourceDescriptions.shop.createOrder" },
                        { "stepId": "notify", "channelPath": "{$sourceDescriptions.events.url}#/channels/orders", "action": "send" },
                        { "stepId": "ship", "operationId": "$sourceDescriptions.partner.ship" }
                    ]
                }
            ]
        }
        """);
        WriteOpenApi("createOrder");
        WriteAsyncApi("orders");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private void WriteOpenApi(string operationId) => File.WriteAllText(Path.Combine(_tempDir, "openapi.json"), $$"""
        { "openapi": "3.1.0", "paths": { "/orders": { "post": { "operationId": "{{operationId}}" } } } }
        """);

    private void WriteAsyncApi(string channel) => File.WriteAllText(Path.Combine(_tempDir, "asyncapi.yaml"), $"""
        asyncapi: 3.0.0
        channels:
          {channel}:
            address: {channel}
        """);

    private ValidateCommandContext Context(Action<ValidateCommandContext> configure)
    {
        var context = new ValidateCommandContext();
        context.Files.Add(_arazzoPath);
        configure(context);
        return context;
    }

    private static (int ExitCode, List<string> Info, List<string> Errors) Run(ValidateCommandContext context)
    {
        var info = new List<string>();
        var errors = new List<string>();
        var exitCode = new ValidateCommandWorker(context, info.Add, _ => { }, errors.Add).Process();
        return (exitCode, info, errors);
    }

    [Fact]
    public void Process_WithMatchingSources_ReturnsZeroAndListsUncheckedSources()
    {
        // Arrange
        var context = Context(c =>
        {
            c.Sources["shop"] = Path.Combine(_tempDir, "openapi.json");
            c.Sources["events"] = Path.Combine(_tempDir, "asyncapi.yaml");
        });

        // Act
        var (exitCode, info, _) = Run(context);

        // Assert
        exitCode.ShouldBe(0);
        info.ShouldContain(line => line.Contains("'partner'") && line.Contains("not checked"));
    }

    [Fact]
    public void Process_RenamedOperation_ReturnsOneAndNamesTheStep()
    {
        // Arrange
        WriteOpenApi("placeOrder");
        var context = Context(c => c.Sources["shop"] = Path.Combine(_tempDir, "openapi.json"));

        // Act
        var (exitCode, _, errors) = Run(context);

        // Assert
        exitCode.ShouldBe(1);
        errors.ShouldContain(line => line.Contains("checkout.order") && line.Contains("createOrder"));
    }

    [Fact]
    public void Process_ResolveLocalSources_LoadsRelativeFiles()
    {
        // Arrange
        WriteAsyncApi("payments");
        var context = Context(c => c.ResolveLocalSources = true);

        // Act
        var (exitCode, _, errors) = Run(context);

        // Assert
        exitCode.ShouldBe(1);
        errors.ShouldContain(line => line.Contains("checkout.notify") && line.Contains("channels/orders"));
    }

    [Fact]
    public void Process_WithoutSourceOptions_DoesNotCheckReferences()
    {
        // Arrange
        WriteOpenApi("placeOrder");
        var context = Context(_ => { });

        // Act
        var (exitCode, _, _) = Run(context);

        // Assert
        exitCode.ShouldBe(0);
    }

    [Fact]
    public void Process_SourceFileMissing_ReturnsOne()
    {
        // Arrange
        var context = Context(c => c.Sources["shop"] = Path.Combine(_tempDir, "nope.json"));

        // Act
        var (exitCode, _, errors) = Run(context);

        // Assert
        exitCode.ShouldBe(1);
        errors.ShouldContain(line => line.Contains("Source 'shop' could not be loaded"));
    }

    [Fact]
    public void Process_SourceFileMalformed_ReturnsOne()
    {
        // Arrange
        var path = Path.Combine(_tempDir, "broken.json");
        File.WriteAllText(path, "{ not json");
        var context = Context(c => c.Sources["shop"] = path);

        // Act
        var (exitCode, _, errors) = Run(context);

        // Assert
        exitCode.ShouldBe(1);
        errors.ShouldContain(line => line.Contains("could not be loaded"));
    }

    [Fact]
    public void Process_GivenSourceOfUnknownKind_ReturnsOne()
    {
        // Arrange
        File.WriteAllText(_arazzoPath, """
        {
            "arazzo": "1.1.0",
            "info": { "title": "t", "version": "1" },
            "sourceDescriptions": [ { "name": "other", "url": "other.json" } ],
            "workflows": [ { "workflowId": "w", "steps": [ { "stepId": "s", "operationId": "x" } ] } ]
        }
        """);
        var path = Path.Combine(_tempDir, "other.json");
        File.WriteAllText(path, """{ "title": "neither" }""");
        var context = Context(c => c.Sources["other"] = path);

        // Act
        var (exitCode, _, errors) = Run(context);

        // Assert
        exitCode.ShouldBe(1);
        errors.ShouldContain(line => line.Contains("neither an OpenAPI nor an AsyncAPI document"));
    }

    [Fact]
    public void Process_UntypedSource_IsDetectedFromTheDocument()
    {
        // Arrange
        File.WriteAllText(_arazzoPath, """
        {
            "arazzo": "1.1.0",
            "info": { "title": "t", "version": "1" },
            "sourceDescriptions": [ { "name": "shop", "url": "openapi.json" } ],
            "workflows": [ { "workflowId": "w", "steps": [ { "stepId": "s", "operationId": "missing" } ] } ]
        }
        """);
        var context = Context(c => c.ResolveLocalSources = true);

        // Act
        var (exitCode, _, errors) = Run(context);

        // Assert
        exitCode.ShouldBe(1);
        errors.ShouldContain(line => line.Contains("did not resolve against any registered source"));
    }

    [Fact]
    public void Process_SourceNoDocumentDeclares_ReturnsOneAndNamesIt()
    {
        // Arrange: a typo for 'shop', so the file it maps is never opened.
        var context = Context(c => c.Sources["shpo"] = Path.Combine(_tempDir, "openapi.json"));

        // Act
        var (exitCode, _, errors) = Run(context);

        // Assert
        exitCode.ShouldBe(1);
        errors.ShouldContain(line => line.Contains("--source 'shpo' is not a source description of any document validated"));
    }

    [Fact]
    public void Process_SourceAnotherDocumentDeclares_IsUsed()
    {
        // Arrange: only the first of the two documents declares 'shop'.
        var other = Path.Combine(_tempDir, "other.arazzo.json");
        File.WriteAllText(other, """
        {
            "arazzo": "1.1.0",
            "info": { "title": "t", "version": "1" },
            "sourceDescriptions": [ { "name": "billing", "url": "https://billing.example.com/openapi.json" } ],
            "workflows": [ { "workflowId": "w", "steps": [ { "stepId": "s", "operationId": "$sourceDescriptions.billing.pay" } ] } ]
        }
        """);
        var context = Context(c =>
        {
            c.Files.Add(other);
            c.Sources["shop"] = Path.Combine(_tempDir, "openapi.json");
        });

        // Act
        var (exitCode, _, errors) = Run(context);

        // Assert
        exitCode.ShouldBe(0);
        errors.ShouldBeEmpty();
    }

    [Fact]
    public void Process_LocalSourceUrlThatIsNoPath_ReportsItAndValidatesTheRest()
    {
        // Arrange: %00 decodes to a NUL, which no path may hold.
        File.WriteAllText(_arazzoPath, """
        {
            "arazzo": "1.1.0",
            "info": { "title": "t", "version": "1" },
            "sourceDescriptions": [
                { "name": "bad", "url": "./bad%00.json", "type": "openapi" },
                { "name": "shop", "url": "openapi.json", "type": "openapi" }
            ],
            "workflows": [ { "workflowId": "w", "steps": [ { "stepId": "s", "operationId": "$sourceDescriptions.shop.missing" } ] } ]
        }
        """);
        var context = Context(c => c.ResolveLocalSources = true);

        // Act
        var (exitCode, _, errors) = Run(context);

        // Assert
        exitCode.ShouldBe(1);
        errors.ShouldContain(line => line.Contains("Source 'bad' url './bad%00.json' is not a usable local path"));
        errors.ShouldContain(line => line.Contains("$sourceDescriptions.shop.missing") && line.Contains("did not resolve"));
    }
}
