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
/// <see cref="MessageAttribute.CorrelationIdLocation"/> must reach the generated message.
/// </summary>
public class MessageAttributeCorrelationIdTests
{
    private const string DocumentName = "message-attribute-correlation-id";

    public sealed record OrderPlaced(string OrderId);

    public sealed record OrderCancelled(string OrderId);

    [AsyncApi(DocumentName)]
    public sealed class Channels
    {
        [Channel("orders/placed")]
        [Message(typeof(OrderPlaced), MessageId = "orderPlaced",
            CorrelationIdLocation = "$message.header#/correlationId",
            CorrelationIdDescription = "Links a reply to its request.")]
        [SubscribeOperation(typeof(OrderPlaced), "orders", OperationId = "orderPlacedOperation")]
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
    public async Task GetAsyncApiDocument_MessageWithCorrelationIdLocation_SetsCorrelationId()
    {
        // Act
        var document = await GetDocumentAsync();

        // Assert
        var correlationId = document.Components.Messages["orderPlaced"].CorrelationId;
        correlationId.ShouldNotBeNull();
        correlationId.Location.ShouldBe("$message.header#/correlationId");
        correlationId.Description.ShouldBe("Links a reply to its request.");
    }

    [Fact]
    public async Task GetAsyncApiDocument_MessageWithoutCorrelationIdLocation_LeavesCorrelationIdEmpty()
    {
        // Act
        var document = await GetDocumentAsync();

        // Assert
        document.Components.Messages["orderCancelled"].CorrelationId.ShouldBeNull();
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
