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
/// <see cref="MessageAttribute.HeadersType"/> and <see cref="MessageAttribute.ContentType"/> must reach the
/// generated message.
/// </summary>
public class MessageAttributeHeadersTests
{
    private const string DocumentName = "message-attribute-headers";

    public sealed record OrderPlaced(string OrderId);

    public sealed record OrderPlacedHeaders(string CorrelationId);

    public sealed record OrderCancelled(string OrderId);

    [AsyncApi(DocumentName)]
    public sealed class Channels
    {
        [Channel("orders/placed")]
        [Message(typeof(OrderPlaced), MessageId = "orderPlaced", HeadersType = typeof(OrderPlacedHeaders), ContentType = "application/cloudevents+json")]
        [SubscribeOperation(OperationId = "orderPlacedOperation")]
        public void OrderPlacedEvent()
        {
        }

        [Channel("orders/cancelled")]
        [Message(typeof(OrderCancelled), MessageId = "orderCancelled")]
        [SubscribeOperation(OperationId = "orderCancelledOperation")]
        public void OrderCancelledEvent()
        {
        }
    }

    [Fact]
    public async Task GetAsyncApiDocument_MessageWithHeadersType_ReferencesHeadersSchema()
    {
        // Act
        var document = await GetDocumentAsync();

        // Assert
        var message = document.Components.Messages["orderPlaced"];
        message.Headers.ShouldNotBeNull();
        message.Headers.Schema.ShouldBeOfType<AsyncApiJsonSchemaReference>();
        document.Components.Schemas.ShouldContainKey("orderPlacedHeaders");
    }

    [Fact]
    public async Task GetAsyncApiDocument_MessageWithContentType_SetsContentType()
    {
        // Act
        var document = await GetDocumentAsync();

        // Assert
        document.Components.Messages["orderPlaced"].ContentType.ShouldBe("application/cloudevents+json");
    }

    [Fact]
    public async Task GetAsyncApiDocument_MessageWithoutHeadersOrContentType_LeavesThemUnset()
    {
        // Act
        var document = await GetDocumentAsync();

        // Assert
        var message = document.Components.Messages["orderCancelled"];
        message.Headers.ShouldBeNull();
        message.ContentType.ShouldBeNull();
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
