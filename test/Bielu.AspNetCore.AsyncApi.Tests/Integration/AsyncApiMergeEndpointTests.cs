// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Text.Json;
using Bielu.AspNetCore.AsyncApi.Extensions;
using Bielu.AspNetCore.AsyncApi.Merger.Extensions;
using Bielu.AspNetCore.AsyncApi.Merger.Merge;
using Bielu.AspNetCore.AsyncApi.Services;
using ByteBard.AsyncAPI.Readers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Tests.Integration;

/// <summary>
/// Integration tests for the merged AsyncAPI document endpoint.
/// </summary>
public class AsyncApiMergeEndpointTests : IAsyncLifetime
{
    private readonly string _tempDir;
    private readonly string _doc1Path;
    private readonly string _doc2Path;

    public AsyncApiMergeEndpointTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"asyncapi_merge_test_{Guid.NewGuid():N}");
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
                "userSignedup": {
                    "address": "user/signedup",
                    "description": "User signup events"
                }
            },
            "operations": {
                "publishUserSignedup": {
                    "action": "send",
                    "summary": "Publish user signup",
                    "channel": { "$ref": "#/channels/userSignedup" }
                }
            }
        }
        """);

        File.WriteAllText(_doc2Path, """
        {
            "asyncapi": "3.0.0",
            "info": { "title": "Service B", "version": "2.0.0" },
            "channels": {
                "orderPlaced": {
                    "address": "order/placed",
                    "description": "Order placed events"
                }
            },
            "operations": {
                "receiveOrderPlaced": {
                    "action": "receive",
                    "summary": "Receive order placed",
                    "channel": { "$ref": "#/channels/orderPlaced" }
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
    public async Task MapMergedAsyncApi_DefaultRoute_ReturnsJsonDocument()
    {
        // Arrange
        using var host = await CreateMergeTestHostAsync(_doc1Path, _doc2Path);
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync("/asyncapi/merged.json");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");
    }

    [Fact]
    public async Task MapMergedAsyncApi_ReturnsValidJsonContent()
    {
        // Arrange
        using var host = await CreateMergeTestHostAsync(_doc1Path, _doc2Path);
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync("/asyncapi/merged.json");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        var action = () => JsonDocument.Parse(content);
        action.ShouldNotThrow();
    }

    [Fact]
    public async Task MapMergedAsyncApi_CombinesChannelsFromMultipleSources()
    {
        // Arrange
        using var host = await CreateMergeTestHostAsync(_doc1Path, _doc2Path);
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync("/asyncapi/merged.json");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        var reader = new AsyncApiStringReader();
        var document = reader.Read(content, out var diagnostic);

        document.ShouldNotBeNull();
        document.Channels.ShouldContainKey("userSignedup");
        document.Channels.ShouldContainKey("orderPlaced");
    }

    [Fact]
    public async Task MapMergedAsyncApi_CombinesOperationsFromMultipleSources()
    {
        // Arrange
        using var host = await CreateMergeTestHostAsync(_doc1Path, _doc2Path);
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync("/asyncapi/merged.json");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        var reader = new AsyncApiStringReader();
        var document = reader.Read(content, out _);

        document.ShouldNotBeNull();
        document.Operations.ShouldContainKey("publishUserSignedup");
        document.Operations.ShouldContainKey("receiveOrderPlaced");
    }

    [Fact]
    public async Task MapMergedAsyncApi_WithPrefixes_PrefixesKeys()
    {
        // Arrange
        using var host = await CreateMergeTestHostWithPrefixesAsync(_doc1Path, _doc2Path);
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync("/asyncapi/merged.json");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        var reader = new AsyncApiStringReader();
        var document = reader.Read(content, out _);

        document.ShouldNotBeNull();
        document.Channels.ShouldContainKey("svcA_userSignedup");
        document.Channels.ShouldContainKey("svcB_orderPlaced");
    }

    [Fact]
    public async Task MapMergedAsyncApi_UsesFirstDocumentInfoByDefault()
    {
        // Arrange
        using var host = await CreateMergeTestHostAsync(_doc1Path, _doc2Path);
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync("/asyncapi/merged.json");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        var reader = new AsyncApiStringReader();
        var document = reader.Read(content, out _);

        document.ShouldNotBeNull();
        document.Info.Title.ShouldBe("Service A");
        document.Info.Version.ShouldBe("1.0.0");
    }

    [Fact]
    public async Task MapMergedAsyncApi_WithCustomInfo_UsesCustomInfo()
    {
        // Arrange
        using var host = await CreateMergeTestHostWithCustomInfoAsync(_doc1Path, _doc2Path);
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync("/asyncapi/merged.json");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        var reader = new AsyncApiStringReader();
        var document = reader.Read(content, out _);

        document.ShouldNotBeNull();
        document.Info.Title.ShouldBe("Microservices Platform");
        document.Info.Version.ShouldBe("1.0.0");
    }

    [Fact]
    public async Task MapMergedAsyncApi_CachesDocument_ReturnsSameOnSecondRequest()
    {
        // Arrange
        using var host = await CreateMergeTestHostAsync(_doc1Path, _doc2Path);
        var client = host.GetTestClient();

        // Act - two consecutive requests
        var response1 = await client.GetAsync("/asyncapi/merged.json");
        var content1 = await response1.Content.ReadAsStringAsync();

        var response2 = await client.GetAsync("/asyncapi/merged.json");
        var content2 = await response2.Content.ReadAsStringAsync();

        // Assert - both should succeed and return the same content (cached)
        response1.StatusCode.ShouldBe(HttpStatusCode.OK);
        response2.StatusCode.ShouldBe(HttpStatusCode.OK);
        content1.ShouldBe(content2);
    }

    [Fact]
    public async Task MapMergedAsyncApi_YamlRoute_ReturnsYamlContentType()
    {
        // Arrange
        using var host = await CreateMergeTestHostAsync(_doc1Path, _doc2Path, pattern: "/asyncapi/merged.yaml");
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync("/asyncapi/merged.yaml");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType!.ShouldContain("yaml");
    }

    private static async Task<IHost> CreateMergeTestHostAsync(string doc1Path, string doc2Path, string pattern = "/asyncapi/merged.json")
    {
        var builder = Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddControllers();
                    services.AddAsyncApiMerge(options =>
                    {
                        options.AddSource(doc1Path);
                        options.AddSource(doc2Path);
                    });
                    services.AddRouting();
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapMergedAsyncApi(pattern);
                    });
                });
            });

        var host = builder.Build();
        await host.StartAsync();
        return host;
    }

    private static async Task<IHost> CreateMergeTestHostWithPrefixesAsync(string doc1Path, string doc2Path)
    {
        var builder = Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddControllers();
                    services.AddAsyncApiMerge(options =>
                    {
                        options.AddSource(doc1Path, "svcA");
                        options.AddSource(doc2Path, "svcB");
                    });
                    services.AddRouting();
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapMergedAsyncApi();
                    });
                });
            });

        var host = builder.Build();
        await host.StartAsync();
        return host;
    }

    private static async Task<IHost> CreateMergeTestHostWithCustomInfoAsync(string doc1Path, string doc2Path)
    {
        var builder = Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddControllers();
                    services.AddAsyncApiMerge(options =>
                    {
                        options.AddSource(doc1Path);
                        options.AddSource(doc2Path);
                        options.Info = new ByteBard.AsyncAPI.Models.AsyncApiInfo
                        {
                            Title = "Microservices Platform",
                            Version = "1.0.0"
                        };
                    });
                    services.AddRouting();
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapMergedAsyncApi();
                    });
                });
            });

        var host = builder.Build();
        await host.StartAsync();
        return host;
    }
}
