// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bielu.AspNetCore.AsyncApi.Merger.Merge;
using ByteBard.AsyncAPI.Models;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="AsyncApiDocumentMerger"/>.
/// </summary>
public class AsyncApiDocumentMergerTests
{
    private static AsyncApiDocument CreateDocument(string title, string version, Dictionary<string, AsyncApiChannel>? channels = null, Dictionary<string, AsyncApiServer>? servers = null, Dictionary<string, AsyncApiOperation>? operations = null)
    {
        return new AsyncApiDocument
        {
            Info = new AsyncApiInfo { Title = title, Version = version },
            Channels = channels ?? new Dictionary<string, AsyncApiChannel>(),
            Servers = servers ?? new Dictionary<string, AsyncApiServer>(),
            Operations = operations ?? new Dictionary<string, AsyncApiOperation>(),
        };
    }

    [Fact]
    public void MergeDocuments_WithSingleDocument_ReturnsSameContent()
    {
        // Arrange
        var doc = CreateDocument("Service A", "1.0.0", channels: new Dictionary<string, AsyncApiChannel>
        {
            ["user/signedup"] = new AsyncApiChannel { Description = "User signup events" }
        });

        var input = new List<(AsyncApiDocument Document, string? KeyPrefix)> { (doc, null) };

        // Act
        var merged = AsyncApiDocumentMerger.MergeDocuments(input);

        // Assert
        merged.Info.Title.ShouldBe("Service A");
        merged.Info.Version.ShouldBe("1.0.0");
        merged.Channels.ShouldContainKey("user/signedup");
        merged.Channels["user/signedup"].Description.ShouldBe("User signup events");
    }

    [Fact]
    public void MergeDocuments_WithMultipleDocuments_CombinesChannels()
    {
        // Arrange
        var doc1 = CreateDocument("Service A", "1.0.0", channels: new Dictionary<string, AsyncApiChannel>
        {
            ["user/signedup"] = new AsyncApiChannel { Description = "User signup events" }
        });

        var doc2 = CreateDocument("Service B", "2.0.0", channels: new Dictionary<string, AsyncApiChannel>
        {
            ["order/placed"] = new AsyncApiChannel { Description = "Order placed events" }
        });

        var input = new List<(AsyncApiDocument Document, string? KeyPrefix)>
        {
            (doc1, null),
            (doc2, null)
        };

        // Act
        var merged = AsyncApiDocumentMerger.MergeDocuments(input);

        // Assert
        merged.Channels.Count.ShouldBe(2);
        merged.Channels.ShouldContainKey("user/signedup");
        merged.Channels.ShouldContainKey("order/placed");
    }

    [Fact]
    public void MergeDocuments_WithKeyPrefix_PrefixesKeys()
    {
        // Arrange
        var doc1 = CreateDocument("Service A", "1.0.0", channels: new Dictionary<string, AsyncApiChannel>
        {
            ["events"] = new AsyncApiChannel { Description = "Service A events" }
        });

        var doc2 = CreateDocument("Service B", "2.0.0", channels: new Dictionary<string, AsyncApiChannel>
        {
            ["events"] = new AsyncApiChannel { Description = "Service B events" }
        });

        var input = new List<(AsyncApiDocument Document, string? KeyPrefix)>
        {
            (doc1, "serviceA"),
            (doc2, "serviceB")
        };

        // Act
        var merged = AsyncApiDocumentMerger.MergeDocuments(input);

        // Assert
        merged.Channels.Count.ShouldBe(2);
        merged.Channels.ShouldContainKey("serviceA_events");
        merged.Channels.ShouldContainKey("serviceB_events");
        merged.Channels["serviceA_events"].Description.ShouldBe("Service A events");
        merged.Channels["serviceB_events"].Description.ShouldBe("Service B events");
    }

    [Fact]
    public void MergeDocuments_CombinesServers()
    {
        // Arrange
        var doc1 = CreateDocument("Service A", "1.0.0", servers: new Dictionary<string, AsyncApiServer>
        {
            ["production"] = new AsyncApiServer { Host = "broker1.example.com", Protocol = "amqp" }
        });

        var doc2 = CreateDocument("Service B", "2.0.0", servers: new Dictionary<string, AsyncApiServer>
        {
            ["staging"] = new AsyncApiServer { Host = "broker2.example.com", Protocol = "kafka" }
        });

        var input = new List<(AsyncApiDocument Document, string? KeyPrefix)>
        {
            (doc1, null),
            (doc2, null)
        };

        // Act
        var merged = AsyncApiDocumentMerger.MergeDocuments(input);

        // Assert
        merged.Servers.Count.ShouldBe(2);
        merged.Servers.ShouldContainKey("production");
        merged.Servers.ShouldContainKey("staging");
    }

    [Fact]
    public void MergeDocuments_CombinesOperations()
    {
        // Arrange
        var doc1 = CreateDocument("Service A", "1.0.0", operations: new Dictionary<string, AsyncApiOperation>
        {
            ["sendUserSignedup"] = new AsyncApiOperation { Summary = "Send user signup" }
        });

        var doc2 = CreateDocument("Service B", "2.0.0", operations: new Dictionary<string, AsyncApiOperation>
        {
            ["sendOrderPlaced"] = new AsyncApiOperation { Summary = "Send order placed" }
        });

        var input = new List<(AsyncApiDocument Document, string? KeyPrefix)>
        {
            (doc1, null),
            (doc2, null)
        };

        // Act
        var merged = AsyncApiDocumentMerger.MergeDocuments(input);

        // Assert
        merged.Operations.Count.ShouldBe(2);
        merged.Operations.ShouldContainKey("sendUserSignedup");
        merged.Operations.ShouldContainKey("sendOrderPlaced");
    }

    [Fact]
    public void MergeDocuments_WithCustomInfo_UsesCustomInfo()
    {
        // Arrange
        var doc = CreateDocument("Service A", "1.0.0");
        var customInfo = new AsyncApiInfo { Title = "Microservices Hub", Version = "3.0.0" };

        var input = new List<(AsyncApiDocument Document, string? KeyPrefix)> { (doc, null) };

        // Act
        var merged = AsyncApiDocumentMerger.MergeDocuments(input, info: customInfo);

        // Assert
        merged.Info.Title.ShouldBe("Microservices Hub");
        merged.Info.Version.ShouldBe("3.0.0");
    }

    [Fact]
    public void MergeDocuments_WithoutCustomInfo_UsesFirstDocumentInfo()
    {
        // Arrange
        var doc1 = CreateDocument("Service A", "1.0.0");
        var doc2 = CreateDocument("Service B", "2.0.0");

        var input = new List<(AsyncApiDocument Document, string? KeyPrefix)>
        {
            (doc1, null),
            (doc2, null)
        };

        // Act
        var merged = AsyncApiDocumentMerger.MergeDocuments(input);

        // Assert
        merged.Info.Title.ShouldBe("Service A");
        merged.Info.Version.ShouldBe("1.0.0");
    }

    [Fact]
    public void MergeDocuments_DuplicateKeysWithoutPrefix_FirstOneWins()
    {
        // Arrange
        var doc1 = CreateDocument("Service A", "1.0.0", channels: new Dictionary<string, AsyncApiChannel>
        {
            ["shared/channel"] = new AsyncApiChannel { Description = "From Service A" }
        });

        var doc2 = CreateDocument("Service B", "2.0.0", channels: new Dictionary<string, AsyncApiChannel>
        {
            ["shared/channel"] = new AsyncApiChannel { Description = "From Service B" }
        });

        var input = new List<(AsyncApiDocument Document, string? KeyPrefix)>
        {
            (doc1, null),
            (doc2, null)
        };

        // Act
        var merged = AsyncApiDocumentMerger.MergeDocuments(input);

        // Assert
        merged.Channels.Count.ShouldBe(1);
        merged.Channels["shared/channel"].Description.ShouldBe("From Service A");
    }

    [Fact]
    public void MergeDocuments_WithEmptyDocuments_ReturnsEmptyMergedDocument()
    {
        // Arrange
        var doc1 = CreateDocument("Service A", "1.0.0");
        var doc2 = CreateDocument("Service B", "2.0.0");

        var input = new List<(AsyncApiDocument Document, string? KeyPrefix)>
        {
            (doc1, null),
            (doc2, null)
        };

        // Act
        var merged = AsyncApiDocumentMerger.MergeDocuments(input);

        // Assert
        merged.Channels.ShouldBeEmpty();
        merged.Servers.ShouldBeEmpty();
        merged.Operations.ShouldBeEmpty();
    }

    [Fact]
    public void MergeDocuments_WithNoDocuments_ThrowsArgumentException()
    {
        // Arrange
        var input = new List<(AsyncApiDocument Document, string? KeyPrefix)>();

        // Act & Assert
        Should.Throw<ArgumentException>(() => AsyncApiDocumentMerger.MergeDocuments(input));
    }

    [Fact]
    public void ParseDocument_ValidV3Json_ParsesSuccessfully()
    {
        // Arrange
        var json = """
        {
            "asyncapi": "3.0.0",
            "info": {
                "title": "Test API",
                "version": "1.0.0"
            },
            "channels": {
                "testChannel": {
                    "address": "test/topic",
                    "description": "A test channel"
                }
            }
        }
        """;

        // Act
        var document = AsyncApiDocumentMerger.ParseDocument(json, "test.json");

        // Assert
        document.ShouldNotBeNull();
        document.Info.Title.ShouldBe("Test API");
        document.Channels.ShouldContainKey("testChannel");
    }

    [Fact]
    public async Task MergeAsync_WithFileSource_LoadsAndMerges()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            var json = """
            {
                "asyncapi": "3.0.0",
                "info": {
                    "title": "File API",
                    "version": "1.0.0"
                },
                "channels": {
                    "fileChannel": {
                        "address": "file/topic",
                        "description": "A file-based channel"
                    }
                }
            }
            """;
            await File.WriteAllTextAsync(tempFile, json);

            var options = new AsyncApiMergeOptions();
            options.AddSource(tempFile);

            using var httpClient = new HttpClient();
            var merger = new AsyncApiDocumentMerger(httpClient);

            // Act
            var merged = await merger.MergeAsync(options);

            // Assert
            merged.Info.Title.ShouldBe("File API");
            merged.Channels.ShouldContainKey("fileChannel");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task MergeAsync_WithMultipleFileSources_MergesAll()
    {
        // Arrange
        var tempFile1 = Path.GetTempFileName();
        var tempFile2 = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile1, """
            {
                "asyncapi": "3.0.0",
                "info": { "title": "API 1", "version": "1.0.0" },
                "channels": {
                    "channel1": { "address": "topic1", "description": "Channel 1" }
                }
            }
            """);
            await File.WriteAllTextAsync(tempFile2, """
            {
                "asyncapi": "3.0.0",
                "info": { "title": "API 2", "version": "2.0.0" },
                "channels": {
                    "channel2": { "address": "topic2", "description": "Channel 2" }
                }
            }
            """);

            var options = new AsyncApiMergeOptions();
            options.AddSource(tempFile1, "svc1");
            options.AddSource(tempFile2, "svc2");

            using var httpClient = new HttpClient();
            var merger = new AsyncApiDocumentMerger(httpClient);

            // Act
            var merged = await merger.MergeAsync(options);

            // Assert
            merged.Channels.Count.ShouldBe(2);
            merged.Channels.ShouldContainKey("svc1_channel1");
            merged.Channels.ShouldContainKey("svc2_channel2");
        }
        finally
        {
            File.Delete(tempFile1);
            File.Delete(tempFile2);
        }
    }

    [Fact]
    public async Task MergeAsync_WithNoSources_ThrowsArgumentException()
    {
        // Arrange
        var options = new AsyncApiMergeOptions();
        using var httpClient = new HttpClient();
        var merger = new AsyncApiDocumentMerger(httpClient);

        // Act & Assert
        await Should.ThrowAsync<ArgumentException>(() => merger.MergeAsync(options));
    }

    [Fact]
    public async Task MergeAsync_WithNonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var options = new AsyncApiMergeOptions();
        options.AddSource("/tmp/nonexistent_asyncapi_doc_12345.json");
        using var httpClient = new HttpClient();
        var merger = new AsyncApiDocumentMerger(httpClient);

        // Act & Assert
        await Should.ThrowAsync<FileNotFoundException>(() => merger.MergeAsync(options));
    }

    [Fact]
    public void MergeOptions_AddSource_AddsSourceCorrectly()
    {
        // Arrange
        var options = new AsyncApiMergeOptions();

        // Act
        options.AddSource("https://example.com/api.json", "svc1");
        options.AddSource("/path/to/local.json");

        // Assert
        options.Sources.Count.ShouldBe(2);
        options.Sources[0].Uri.ShouldBe("https://example.com/api.json");
        options.Sources[0].KeyPrefix.ShouldBe("svc1");
        options.Sources[1].Uri.ShouldBe("/path/to/local.json");
        options.Sources[1].KeyPrefix.ShouldBeNull();
    }

    [Fact]
    public void MergeOptions_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var options = new AsyncApiMergeOptions();

        // Assert
        options.CacheDuration.ShouldBe(TimeSpan.FromMinutes(5));
        options.HttpTimeout.ShouldBe(TimeSpan.FromSeconds(30));
        options.Sources.ShouldBeEmpty();
        options.Info.ShouldBeNull();
        options.DefaultContentType.ShouldBeNull();
    }

    [Fact]
    public void MergeDocuments_WithComponents_MergesComponents()
    {
        // Arrange
        var doc1 = new AsyncApiDocument
        {
            Info = new AsyncApiInfo { Title = "Service A", Version = "1.0.0" },
            Components = new AsyncApiComponents
            {
                Messages = new Dictionary<string, AsyncApiMessage>
                {
                    ["UserSignedUp"] = new AsyncApiMessage { Description = "User signed up event" }
                }
            }
        };

        var doc2 = new AsyncApiDocument
        {
            Info = new AsyncApiInfo { Title = "Service B", Version = "2.0.0" },
            Components = new AsyncApiComponents
            {
                Messages = new Dictionary<string, AsyncApiMessage>
                {
                    ["OrderPlaced"] = new AsyncApiMessage { Description = "Order placed event" }
                }
            }
        };

        var input = new List<(AsyncApiDocument Document, string? KeyPrefix)>
        {
            (doc1, null),
            (doc2, null)
        };

        // Act
        var merged = AsyncApiDocumentMerger.MergeDocuments(input);

        // Assert
        merged.Components.ShouldNotBeNull();
        merged.Components.Messages.ShouldNotBeNull();
        merged.Components.Messages.Count.ShouldBe(2);
        merged.Components.Messages.ShouldContainKey("UserSignedUp");
        merged.Components.Messages.ShouldContainKey("OrderPlaced");
    }
}
