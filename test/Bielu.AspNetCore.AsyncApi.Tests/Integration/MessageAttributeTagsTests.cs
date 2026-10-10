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
/// <see cref="MessageAttribute.Tags"/> must reach the generated message.
/// </summary>
public class MessageAttributeTagsTests
{
    private const string DocumentName = "message-attribute-tags";

    public sealed record OrderPlaced(string OrderId);

    public sealed record OrderCancelled(string OrderId);

    [AsyncApi(DocumentName)]
    public sealed class Channels
    {
        [Channel("orders/placed")]
        [Message(typeof(OrderPlaced), "orders", "billing", MessageId = "orderPlaced")]
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
    public async Task GetAsyncApiDocument_MessageWithTags_AddsTagsToMessage()
    {
        // Act
        var document = await GetDocumentAsync();

        // Assert
        var tags = document.Components.Messages["orderPlaced"].Tags;
        tags.ShouldNotBeNull();
        tags.Select(tag => tag.Name).ShouldBe(["orders", "billing"]);
    }

    [Fact]
    public async Task GetAsyncApiDocument_MessageWithTags_RegistersEachTagOnceInComponents()
    {
        // Act
        var document = await GetDocumentAsync();

        // Assert
        document.Components.Tags.Keys.ShouldBe(["orders", "billing"], ignoreOrder: true);
    }

    [Fact]
    public async Task GetAsyncApiDocument_MessageWithoutTags_LeavesTagsEmpty()
    {
        // Act
        var document = await GetDocumentAsync();

        // Assert
        (document.Components.Messages["orderCancelled"].Tags ?? []).ShouldBeEmpty();
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
