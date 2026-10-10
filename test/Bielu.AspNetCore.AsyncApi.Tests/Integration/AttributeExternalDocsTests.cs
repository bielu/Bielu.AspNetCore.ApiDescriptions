using Bielu.AspNetCore.AsyncApi.Attributes.Attributes;
using Bielu.AspNetCore.AsyncApi.Extensions;
using Bielu.AspNetCore.AsyncApi.Services;
using ByteBard.AsyncAPI.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Tests.Integration;

/// <summary>
/// <c>ExternalDocsUrl</c>/<c>ExternalDocsDescription</c> must reach the generated channel, operation and message.
/// </summary>
public class AttributeExternalDocsTests
{
    private const string DocumentName = "attribute-external-docs";

    public sealed record OrderPlaced(string OrderId);

    [AsyncApi(DocumentName)]
    public sealed class Channels
    {
        [Channel("orders/placed", ExternalDocsUrl = "https://example.com/channel", ExternalDocsDescription = "Channel docs")]
        [Message(typeof(OrderPlaced), MessageId = "orderPlaced", ExternalDocsUrl = "https://example.com/message")]
        [SubscribeOperation(typeof(OrderPlaced), OperationId = "orderPlacedOperation", ExternalDocsUrl = "https://example.com/operation", ExternalDocsDescription = "Operation docs")]
        public void OrderPlacedEvent()
        {
        }
    }

    [Fact]
    public async Task GetAsyncApiDocument_ChannelWithExternalDocs_SetsExternalDocs()
    {
        // Act
        var document = await GetDocumentAsync();

        // Assert
        var docs = document.Channels.Values.Single(c => c.Address == "orders/placed").ExternalDocs;
        docs.ShouldNotBeNull();
        docs.Url.ToString().ShouldBe("https://example.com/channel");
        docs.Description.ShouldBe("Channel docs");
    }

    [Fact]
    public async Task GetAsyncApiDocument_OperationWithExternalDocs_SetsExternalDocs()
    {
        // Act
        var document = await GetDocumentAsync();

        // Assert
        var docs = document.Operations["orderPlacedOperation"].ExternalDocs;
        docs.ShouldNotBeNull();
        docs.Url.ToString().ShouldBe("https://example.com/operation");
        docs.Description.ShouldBe("Operation docs");
    }

    [Fact]
    public async Task GetAsyncApiDocument_MessageWithExternalDocs_SetsExternalDocs()
    {
        // Act
        var document = await GetDocumentAsync();

        // Assert
        var docs = document.Components.Messages["orderPlaced"].ExternalDocs;
        docs.ShouldNotBeNull();
        docs.Url.ToString().ShouldBe("https://example.com/message");
        docs.Description.ShouldBeNull();
    }

    private static async Task<AsyncApiDocument> GetDocumentAsync()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAsyncApi(DocumentName);

        await using var app = builder.Build();
        await app.StartAsync();

        var document = await app.Services.GetRequiredKeyedService<IAsyncApiDocumentProvider>(DocumentName)
            .GetAsyncApiDocumentAsync();
        await app.StopAsync();

        return document;
    }
}
