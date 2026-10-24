using Bielu.AspNetCore.AsyncApi.Attributes;
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
/// Bindings registered through <see cref="AsyncApiOptions.AddChannelBinding"/> must reach the channel of the
/// same name, unless that channel names other bindings through <c>BindingsRef</c>.
/// </summary>
public class ChannelBindingsByNameTests
{
    private const string DocumentName = "channel-bindings-by-name";

    public sealed record StockChanged(string Sku);

    [AsyncApi(DocumentName)]
    public sealed class Channels
    {
        [Channel("stock/changed")]
        [ReceiveOperation(typeof(StockChanged), OperationId = "stockChanged")]
        public void Named()
        {
        }

        [Channel("stock/audited", BindingsRef = "audit")]
        [ReceiveOperation(typeof(StockChanged), OperationId = "stockAudited")]
        public void Referenced()
        {
        }

        [Channel("stock/archived", BindingsRef = "not-registered")]
        [ReceiveOperation(typeof(StockChanged), OperationId = "stockArchived")]
        public void UnknownReference()
        {
        }

        [Channel("stock/plain")]
        [ReceiveOperation(typeof(StockChanged), OperationId = "stockPlain")]
        public void Unbound()
        {
        }
    }

    [Fact]
    public async Task GetAsyncApiDocument_ChannelBindingNamedAfterChannel_AttachesBindings()
    {
        // Act
        var document = await GetDocumentAsync();

        // Assert
        TopicOf(document.Channels["stockchanged"]).ShouldBe("by-name");
    }

    [Fact]
    public async Task GetAsyncApiDocument_ChannelWithBindingsRef_PrefersReferenceOverName()
    {
        // Act
        var document = await GetDocumentAsync();

        // Assert
        TopicOf(document.Channels["stockaudited"]).ShouldBe("by-ref");
    }

    [Fact]
    public async Task GetAsyncApiDocument_ChannelWithUnregisteredBindingsRef_DoesNotFallBackToName()
    {
        // Act
        var document = await GetDocumentAsync();

        // Assert
        (document.Channels["stockarchived"].Bindings?.Count ?? 0).ShouldBe(0);
    }

    [Fact]
    public async Task GetAsyncApiDocument_ChannelWithoutRegisteredBindings_LeavesBindingsEmpty()
    {
        // Act
        var document = await GetDocumentAsync();

        // Assert
        (document.Channels["stockplain"].Bindings?.Count ?? 0).ShouldBe(0);
    }

    private static string? TopicOf(AsyncApiChannel channel)
        => channel.Bindings.Values.ShouldHaveSingleItem().ShouldBeOfType<KafkaChannelBinding>().Topic;

    private static async Task<AsyncApiDocument> GetDocumentAsync()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAsyncApi(DocumentName, options => options
            .AddChannelBinding("stock/changed", new KafkaChannelBinding { Topic = "by-name" })
            .AddChannelBinding("audit", new KafkaChannelBinding { Topic = "by-ref" })
            .AddChannelBinding("stock/audited", new KafkaChannelBinding { Topic = "shadowed" })
            .AddChannelBinding("stock/archived", new KafkaChannelBinding { Topic = "shadowed" }));

        await using var app = builder.Build();
        await app.StartAsync();

        var document = await app.Services.GetRequiredKeyedService<IAsyncApiDocumentProvider>(DocumentName)
            .GetAsyncApiDocumentAsync();
        await app.StopAsync();

        return document;
    }
}
