using System.Text.Json;
using Bielu.AspNetCore.AsyncApi.Attributes.Attributes;
using Bielu.AspNetCore.AsyncApi.Extensions;
using Bielu.AspNetCore.AsyncApi.Services;
using ByteBard.AsyncAPI.Bindings.Pulsar;
using ByteBard.AsyncAPI.Bindings.Sns;
using ByteBard.AsyncAPI.Bindings.Sqs;
using ByteBard.AsyncAPI.Bindings.WebSockets;
using ByteBard.AsyncAPI.Models.Bindings.Pulsar;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Tests.Integration;

/// <summary>
/// The WebSockets, Pulsar, SNS and SQS bindings shipped by <c>ByteBard.AsyncAPI.NET.Bindings</c> must reach
/// the served document. <see cref="AsyncApiOfficialSchemaConformanceTests"/> validates the Pulsar, SNS and SQS
/// document against the official schemas.
/// </summary>
public class ProtocolBindingsCoverageTests
{
    internal const string DocumentName = "protocol-bindings-coverage";

    // Kept out of the schema-validated document: ByteBard.AsyncAPI.NET.Bindings 3.0.1 writes the WebSockets
    // binding under "websockets", while the AsyncAPI bindings specification and the official schema use "ws".
    private const string WebSocketsDocumentName = "protocol-bindings-websockets";

    public sealed record PriceChanged(string Sku, decimal Price);

    [AsyncApi(WebSocketsDocumentName)]
    public sealed class WebSocketsChannels
    {
        [Channel("prices/ws", BindingsRef = "prices-ws")]
        [PublishOperation(typeof(PriceChanged), OperationId = "pricesOverWebSockets")]
        public void WebSockets()
        {
        }
    }

    [AsyncApi(DocumentName)]
    public sealed class Channels
    {
        [Channel("prices/pulsar", BindingsRef = "prices-pulsar")]
        [PublishOperation(typeof(PriceChanged), OperationId = "pricesOverPulsar")]
        public void Pulsar()
        {
        }

        [Channel("prices/sns", BindingsRef = "prices-sns")]
        [PublishOperation(typeof(PriceChanged), OperationId = "pricesOverSns", BindingsRef = "prices-sns-publish")]
        public void Sns()
        {
        }

        [Channel("prices/sqs", BindingsRef = "prices-sqs")]
        [PublishOperation(typeof(PriceChanged), OperationId = "pricesOverSqs", BindingsRef = "prices-sqs-publish")]
        public void Sqs()
        {
        }
    }

    internal static void Configure(AsyncApiOptions options) => options
        .AddServer("pulsar-cluster", "localhost:6650", "pulsar")
        .AddServerBinding("pulsar-cluster", new PulsarServerBinding { Tenant = "pricing" })
        .AddChannelBinding("prices-pulsar", new PulsarChannelBinding
        {
            Namespace = "retail",
            Persistence = Persistence.Persistent,
        })
        .AddChannelBinding("prices-sns", new SnsChannelBinding { Name = "price-changes" })
        .AddOperationBinding("prices-sns-publish", new SnsOperationBinding
        {
            Consumers =
            [
                new Consumer
                {
                    Protocol = Protocol.Sqs,
                    Endpoint = new ByteBard.AsyncAPI.Bindings.Sns.Identifier { Name = "price-changes-queue.fifo" },
                    RawMessageDelivery = true,
                },
            ],
        })
        // FifoQueue = true on purpose: the official schema requires "fifoQueue", and ByteBard 3.0.1 omits it
        // when false, so a standard queue currently serializes to a document the schema rejects.
        .AddChannelBinding("prices-sqs", new SqsChannelBinding
        {
            Queue = new Queue { Name = "price-changes-queue.fifo", FifoQueue = true },
        })
        .AddOperationBinding("prices-sqs-publish", new SqsOperationBinding
        {
            Queues = [new Queue { Name = "price-changes-queue.fifo", FifoQueue = true }],
        });

    [Theory]
    [InlineData("pricespulsar", "pulsar")]
    [InlineData("pricessns", "sns")]
    [InlineData("pricessqs", "sqs")]
    public async Task GetAsyncApiDocument_ChannelBinding_IsServedUnderProtocolKey(string channelKey, string protocol)
    {
        // Act
        using var document = await GetServedDocumentAsync();

        // Assert
        document.RootElement.GetProperty("channels").GetProperty(channelKey).GetProperty("bindings")
            .TryGetProperty(protocol, out _).ShouldBeTrue();
    }

    [Theory]
    [InlineData("pricesOverSns", "sns")]
    [InlineData("pricesOverSqs", "sqs")]
    public async Task GetAsyncApiDocument_OperationBinding_IsServedUnderProtocolKey(string operationKey, string protocol)
    {
        // Act
        using var document = await GetServedDocumentAsync();

        // Assert
        document.RootElement.GetProperty("operations").GetProperty(operationKey).GetProperty("bindings")
            .TryGetProperty(protocol, out _).ShouldBeTrue();
    }

    [Fact]
    public async Task GetAsyncApiDocument_PulsarServerBinding_IsServedWithTenant()
    {
        // Act
        using var document = await GetServedDocumentAsync();

        // Assert
        document.RootElement.GetProperty("servers").GetProperty("pulsar-cluster").GetProperty("bindings")
            .GetProperty("pulsar").GetProperty("tenant").GetString().ShouldBe("pricing");
    }

    [Fact]
    public async Task GetAsyncApiDocument_WebSocketsChannelBinding_IsServedWithMethod()
    {
        // Act
        using var document = await GetServedDocumentAsync(WebSocketsDocumentName, options => options
            .AddChannelBinding("prices-ws", new WebSocketsChannelBinding { Method = "GET" }));

        // Assert
        var binding = document.RootElement.GetProperty("channels").GetProperty("pricesws").GetProperty("bindings")
            .EnumerateObject().ShouldHaveSingleItem();
        binding.Value.GetProperty("method").GetString().ShouldBe("GET");
    }

    private static Task<JsonDocument> GetServedDocumentAsync() => GetServedDocumentAsync(DocumentName, Configure);

    private static async Task<JsonDocument> GetServedDocumentAsync(string documentName, Action<AsyncApiOptions> configure)
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers();
        builder.Services.AddAsyncApi(documentName, configure);

        await using var app = builder.Build();
        app.MapAsyncApi();
        await app.StartAsync();

        var json = await app.GetTestClient().GetStringAsync(
            AsyncApiGeneratorConstants.DefaultAsyncApiRoute.Replace("{documentName}", documentName));
        await app.StopAsync();

        return JsonDocument.Parse(json);
    }
}
