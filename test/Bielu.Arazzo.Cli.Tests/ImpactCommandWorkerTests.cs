// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bielu.Arazzo.Cli.Commands;
using Shouldly;
using Xunit;

namespace Bielu.Arazzo.Cli.Tests;

public class ImpactCommandWorkerTests : IDisposable
{
    private const string BaseAsyncApi = """
        {
            "asyncapi": "3.0.0",
            "channels": {
                "orders": { "address": "orders", "messages": { "placed": { "$ref": "#/components/messages/OrderPlaced" } } },
                "audit": { "address": "audit" }
            },
            "components": {
                "messages": { "OrderPlaced": { "payload": { "$ref": "#/components/schemas/Order" } } },
                "schemas": { "Order": { "type": "object", "properties": { "sku": { "type": "string" } } } }
            }
        }
        """;

    private const string BaseOpenApi = """
        { "openapi": "3.1.0", "paths": { "/orders": { "post": { "operationId": "createOrder" } } } }
        """;

    private readonly string _tempDir;
    private readonly string _arazzoPath;

    public ImpactCommandWorkerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"arazzo_cli_impact_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _arazzoPath = Write("workflows.arazzo.json", """
        {
            "arazzo": "1.1.0",
            "info": { "title": "Checkout", "version": "1.0.0" },
            "sourceDescriptions": [
                { "name": "shop", "url": "openapi.json", "type": "openapi" },
                { "name": "events", "url": "asyncapi.json", "type": "asyncapi" }
            ],
            "workflows": [
                {
                    "workflowId": "checkout",
                    "steps": [
                        { "stepId": "order", "operationId": "$sourceDescriptions.shop.createOrder" },
                        { "stepId": "notify", "channelPath": "{$sourceDescriptions.events.url}#/channels/orders", "action": "send" },
                        { "stepId": "audit", "channelPath": "{$sourceDescriptions.events.url}#/channels/audit", "action": "send" }
                    ]
                }
            ]
        }
        """);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private string Write(string name, string content)
    {
        var path = Path.Combine(_tempDir, name);
        File.WriteAllText(path, content);
        return path;
    }

    private (int ExitCode, List<string> Info, List<string> Warnings, List<string> Errors) Run(
        string headAsyncApi, string? headOpenApi = null, Action<ImpactCommandContext>? configure = null)
    {
        var context = new ImpactCommandContext { File = _arazzoPath, FailOnBreaking = true };
        context.BaseSources["shop"] = Write("base-openapi.json", BaseOpenApi);
        context.BaseSources["events"] = Write("base-asyncapi.json", BaseAsyncApi);
        context.HeadSources["shop"] = Write("head-openapi.json", headOpenApi ?? BaseOpenApi);
        context.HeadSources["events"] = Write("head-asyncapi.json", headAsyncApi);
        configure?.Invoke(context);

        var (info, warnings, errors) = (new List<string>(), new List<string>(), new List<string>());
        var exitCode = new ImpactCommandWorker(context, info.Add, warnings.Add, errors.Add).Process();
        return (exitCode, info, warnings, errors);
    }

    [Fact]
    public void Process_NoChange_ReportsNothing()
    {
        // Act
        var (exitCode, info, _, errors) = Run(BaseAsyncApi);

        // Assert
        exitCode.ShouldBe(0);
        errors.ShouldBeEmpty();
        info.ShouldContain("No changes detected.");
    }

    [Fact]
    public void Process_RemovedChannel_IsBreaking()
    {
        // Arrange
        var head = BaseAsyncApi.Replace("\"audit\": { \"address\": \"audit\" }", "\"auditLog\": { \"address\": \"audit\" }");

        // Act
        var (exitCode, _, _, errors) = Run(head);

        // Assert
        exitCode.ShouldBe(1);
        errors.ShouldHaveSingleItem().ShouldContain("checkout.audit");
    }

    [Fact]
    public void Process_RenamedOperation_IsBreaking()
    {
        // Act
        var (exitCode, _, _, errors) = Run(BaseAsyncApi, BaseOpenApi.Replace("createOrder", "placeOrder"));

        // Assert
        exitCode.ShouldBe(1);
        errors.ShouldHaveSingleItem().ShouldContain("checkout.order");
    }

    [Fact]
    public void Process_PayloadSchemaChangedBehindRefs_IsListedForReview()
    {
        // Arrange
        var head = BaseAsyncApi.Replace("\"sku\": { \"type\": \"string\" }", "\"sku\": { \"type\": \"integer\" }");

        // Act
        var (exitCode, info, _, errors) = Run(head);

        // Assert
        exitCode.ShouldBe(0);
        errors.ShouldBeEmpty();
        info.ShouldContain(line => line.Contains("checkout.notify") && line.Contains("changed"));
        info.ShouldNotContain(line => line.Contains("checkout.audit"));
    }

    [Fact]
    public void Process_OnlyDescriptionChanged_ReportsNothing()
    {
        // Arrange
        var head = BaseAsyncApi.Replace("\"type\": \"object\",", "\"type\": \"object\", \"description\": \"An order\",");

        // Act
        var (exitCode, info, _, _) = Run(head);

        // Assert
        exitCode.ShouldBe(0);
        info.ShouldContain("No changes detected.");
    }

    [Fact]
    public void Process_ReferenceThatNowResolves_IsNonBreaking()
    {
        // Arrange: the base lacks the audit channel, the head adds it.
        var context = (Action<ImpactCommandContext>)(c =>
            c.BaseSources["events"] = Write("base-asyncapi-old.json", BaseAsyncApi.ReplaceLineEndings("\n").Replace(",\n        \"audit\": { \"address\": \"audit\" }", "")));

        // Act
        var (exitCode, info, _, _) = Run(BaseAsyncApi, configure: context);

        // Assert
        exitCode.ShouldBe(0);
        info.ShouldContain(line => line.Contains("checkout.audit") && line.Contains("resolves after the change"));
    }

    [Fact]
    public void Process_ReferenceBrokenOnBothSides_IsAWarningNotAChange()
    {
        // Arrange
        var head = BaseAsyncApi.Replace("\"audit\": { \"address\": \"audit\" }", "\"x\": { }");
        var context = (Action<ImpactCommandContext>)(c => c.BaseSources["events"] = Write("base-broken.json", head));

        // Act
        var (exitCode, info, warnings, _) = Run(head, configure: context);

        // Assert
        exitCode.ShouldBe(0);
        warnings.ShouldContain(line => line.Contains("checkout.audit") && line.Contains("before or after"));
        info.ShouldContain("No changes detected.");
    }

    [Fact]
    public void Process_WithoutFailOnBreaking_ReturnsZero()
    {
        // Arrange
        var head = BaseAsyncApi.Replace("\"audit\": { \"address\": \"audit\" }", "\"x\": { }");

        // Act
        var (exitCode, _, _, errors) = Run(head, configure: c => c.FailOnBreaking = false);

        // Assert
        exitCode.ShouldBe(0);
        errors.ShouldNotBeEmpty();
    }

    [Fact]
    public void Process_SourceOnOneSideOnly_Warns()
    {
        // Act
        var (_, _, warnings, _) = Run(BaseAsyncApi, configure: c => c.HeadSources.Remove("shop"));

        // Assert
        warnings.ShouldContain(line => line.Contains("'shop'") && line.Contains("only one of --base and --head"));
    }

    [Fact]
    public void Process_UnreadableSource_ReturnsOne()
    {
        // Act
        var (exitCode, _, _, errors) = Run("{ not json");

        // Assert
        exitCode.ShouldBe(1);
        errors.ShouldContain(line => line.Contains("could not be loaded"));
    }

    [Fact]
    public void Process_MissingArazzoFile_ReturnsOne()
    {
        // Act
        var (exitCode, _, _, errors) = Run(BaseAsyncApi, configure: c => c.File = Path.Combine(_tempDir, "nope.json"));

        // Assert
        exitCode.ShouldBe(1);
        errors.ShouldContain(line => line.Contains("File not found"));
    }

    [Fact]
    public void Process_UnparseableArazzoFile_ReturnsOne()
    {
        // Act
        var (exitCode, _, _, errors) = Run(BaseAsyncApi, configure: c => c.File = Write("bad.json", "{ \"arazzo\": "));

        // Assert
        exitCode.ShouldBe(1);
        errors.ShouldNotBeEmpty();
    }

    [Fact]
    public void Constructor_ThrowsForNullArguments()
    {
        var context = new ImpactCommandContext();
        Should.Throw<ArgumentNullException>(() => new ImpactCommandWorker(null!, _ => { }, _ => { }, _ => { }));
        Should.Throw<ArgumentNullException>(() => new ImpactCommandWorker(context, null!, _ => { }, _ => { }));
        Should.Throw<ArgumentNullException>(() => new ImpactCommandWorker(context, _ => { }, null!, _ => { }));
        Should.Throw<ArgumentNullException>(() => new ImpactCommandWorker(context, _ => { }, _ => { }, null!));
    }
}
