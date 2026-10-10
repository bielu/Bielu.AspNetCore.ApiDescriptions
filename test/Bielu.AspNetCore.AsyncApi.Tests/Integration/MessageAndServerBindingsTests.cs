using Bielu.AspNetCore.AsyncApi.Attributes.Attributes;
using Bielu.AspNetCore.AsyncApi.Extensions;
using Bielu.AspNetCore.AsyncApi.Services;
using ByteBard.AsyncAPI.Bindings.Kafka;
using ByteBard.AsyncAPI.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Tests.Integration;

/// <summary>
/// Bindings registered through <see cref="AsyncApiOptions.AddMessageBinding"/> and
/// <see cref="AsyncApiOptions.AddServerBinding"/> must reach the generated document.
/// </summary>
public class MessageAndServerBindingsTests
{
    internal const string DocumentName = "message-server-bindings";

    public sealed record OrderPlaced(string OrderId);

    public sealed record OrderCancelled(string OrderId);

    [AsyncApi(DocumentName)]
    public sealed class Channels
    {
        [Channel("orders/placed")]
        [Message(typeof(OrderPlaced), MessageId = "orderPlaced", BindingsRef = "order placed")]
        [SubscribeOperation(OperationId = "orderPlacedOperation")]
        public void OrderPlacedEvent()
        {
        }

        [Channel("orders/cancelled")]
        [Message(typeof(OrderCancelled), MessageId = "orderCancelled", BindingsRef = "not-registered")]
        [SubscribeOperation(OperationId = "orderCancelledOperation")]
        public void OrderCancelledEvent()
        {
        }
    }

    internal static void Configure(AsyncApiOptions options) => options
        .AddServer("broker", "localhost:9092", "kafka")
        .AddServer("plain", "localhost:9093", "kafka")
        .AddMessageBinding("order placed", new KafkaMessageBinding { SchemaLookupStrategy = "TopicIdStrategy" })
        .AddServerBinding("broker", new KafkaServerBinding { SchemaRegistryVendor = "confluent" })
        .AddServerBinding("unknown-server", new KafkaServerBinding { SchemaRegistryVendor = "apicurio" });

    [Fact]
    public async Task GetAsyncApiDocument_MessageWithRegisteredBindingsRef_AttachesBindings()
    {
        // Act
        var document = await GetDocumentAsync();

        // Assert
        var binding = document.Components.Messages["orderPlaced"].Bindings.Values.ShouldHaveSingleItem();
        binding.ShouldBeOfType<KafkaMessageBinding>().SchemaLookupStrategy.ShouldBe("TopicIdStrategy");
        document.Components.MessageBindings.ShouldContainKey("orderplaced");
    }

    [Fact]
    public async Task GetAsyncApiDocument_MessageWithUnregisteredBindingsRef_LeavesBindingsEmpty()
    {
        // Act
        var document = await GetDocumentAsync();

        // Assert
        (document.Components.Messages["orderCancelled"].Bindings?.Count ?? 0).ShouldBe(0);
    }

    [Fact]
    public async Task GetAsyncApiDocument_ServerBindingForKnownServer_AttachesBindings()
    {
        // Act
        var document = await GetDocumentAsync();

        // Assert
        var binding = document.Servers["broker"].Bindings.Values.ShouldHaveSingleItem();
        binding.ShouldBeOfType<KafkaServerBinding>().SchemaRegistryVendor.ShouldBe("confluent");
        (document.Servers["plain"].Bindings?.Count ?? 0).ShouldBe(0);
    }

    [Fact]
    public async Task GetAsyncApiDocument_ServerBindingForUnknownServer_IsRegisteredInComponentsOnly()
    {
        // Act
        var document = await GetDocumentAsync();

        // Assert
        document.Components.ServerBindings.Keys.ShouldBe(["broker", "unknown-server"], ignoreOrder: true);
        document.Servers.ShouldNotContainKey("unknown-server");
    }

    private static async Task<AsyncApiDocument> GetDocumentAsync()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAsyncApi(DocumentName, Configure);

        await using var app = builder.Build();
        await app.StartAsync();

        var document = await app.Services.GetRequiredKeyedService<IAsyncApiDocumentProvider>(DocumentName)
            .GetAsyncApiDocumentAsync();
        await app.StopAsync();

        return document;
    }
}
