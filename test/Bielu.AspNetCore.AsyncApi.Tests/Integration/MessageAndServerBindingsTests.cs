using Bielu.AspNetCore.AsyncApi.Attributes.Attributes;
using Bielu.AspNetCore.AsyncApi.Extensions;
using Bielu.AspNetCore.AsyncApi.Services;
using ByteBard.AsyncAPI.Bindings.Kafka;
using ByteBard.AsyncAPI.Bindings.MQTT;
using ByteBard.AsyncAPI.Models;
using ByteBard.AsyncAPI.Models.Interfaces;
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

    [Fact]
    public async Task GetAsyncApiDocument_ServerBindingForKnownServer_DoesNotMutateOptionsServer()
    {
        // Arrange
        AsyncApiServer? optionsServer = null;
        void ConfigureCapture(AsyncApiOptions options) => options
            .AddServer("broker", "localhost:9092", "kafka", null, s => optionsServer = s)
            .AddServerBinding("broker", new KafkaServerBinding { SchemaRegistryVendor = "confluent" });

        // Act
        var first = await GetDocumentAsync(ConfigureCapture);
        var second = await GetDocumentAsync(ConfigureCapture);

        // Assert
        optionsServer.ShouldNotBeNull();
        (optionsServer.Bindings?.Count ?? 0).ShouldBe(0);
        first.Servers["broker"].ShouldNotBeSameAs(optionsServer);
        first.Servers["broker"].Bindings.Values.ShouldHaveSingleItem();
        second.Servers["broker"].Bindings.Values.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task GetAsyncApiDocument_ServerBindingAddedToConfiguredServer_PreservesBindingsSetOnServer()
    {
        // Arrange
        void ConfigureMerge(AsyncApiOptions options) => options
            .AddServer("broker", "localhost:9092", "kafka", null, s =>
                s.Bindings = new AsyncApiBindings<IServerBinding> { new KafkaServerBinding { SchemaRegistryVendor = "own" } })
            .AddServerBinding("broker", new MQTTServerBinding { ClientId = "registered" });

        // Act
        var document = await GetDocumentAsync(ConfigureMerge);

        // Assert
        var bindings = document.Servers["broker"].Bindings;
        bindings.Count.ShouldBe(2);
        bindings.Values.OfType<KafkaServerBinding>().Single().SchemaRegistryVendor.ShouldBe("own");
        bindings.Values.OfType<MQTTServerBinding>().Single().ClientId.ShouldBe("registered");
    }

    private static Task<AsyncApiDocument> GetDocumentAsync() => GetDocumentAsync(Configure);

    private static async Task<AsyncApiDocument> GetDocumentAsync(Action<AsyncApiOptions> configure)
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAsyncApi(DocumentName, configure);

        await using var app = builder.Build();
        await app.StartAsync();

        var document = await app.Services.GetRequiredKeyedService<IAsyncApiDocumentProvider>(DocumentName)
            .GetAsyncApiDocumentAsync();
        await app.StopAsync();

        return document;
    }
}
