using System.Text.Json;
using Bielu.AspNetCore.AsyncApi.Attributes;
using Bielu.AspNetCore.AsyncApi.Extensions;
using Bielu.AspNetCore.AsyncApi.Services;
using ByteBard.AsyncAPI.Bindings.Kafka;
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
/// The WebSockets, Pulsar, SNS, SQS and Kafka bindings shipped by <c>ByteBard.AsyncAPI.NET.Bindings</c> must reach
/// the served document. <see cref="AsyncApiOfficialSchemaConformanceTests"/> validates this document against the
/// official schemas, which is what catches the binding serialization bugs fixed in ByteBard 3.1.0-beta.19: the
/// WebSockets key (<c>ws</c>), SQS <c>fifoQueue</c> on a standard queue, and Kafka <c>schemaIdLocation</c> casing.
/// </summary>
public class ProtocolBindingsCoverageTests
{
    internal const string DocumentName = "protocol-bindings-coverage";

    public sealed record PriceChanged(string Sku, decimal Price);

    [AsyncApi(DocumentName)]
    public sealed class Channels
    {
        [Channel("prices/ws", BindingsRef = "prices-ws")]
        [PublishOperation(typeof(PriceChanged), OperationId = "pricesOverWebSockets")]
        public void WebSockets()
        {
        }

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

        [Channel("prices/kafka")]
        [Message(typeof(PriceChanged), MessageId = "priceChangedOverKafka", BindingsRef = "prices-kafka-message")]
        [PublishOperation(OperationId = "pricesOverKafka")]
        public void Kafka()
        {
        }
    }

    internal static void Configure(AsyncApiOptions options) => options
        .AddServer("pulsar-cluster", "localhost:6650", "pulsar")
        .AddServerBinding("pulsar-cluster", new PulsarServerBinding { Tenant = "pricing" })
        .AddChannelBinding("prices-ws", new WebSocketsChannelBinding { Method = "GET" })
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
        // A standard queue on the channel and a FIFO one on the operation: the official schema requires
        // "fifoQueue" either way, and ByteBard 3.0.1 omitted it when false.
        .AddChannelBinding("prices-sqs", new SqsChannelBinding
        {
            Queue = new Queue { Name = "price-changes-queue", FifoQueue = false },
        })
        .AddOperationBinding("prices-sqs-publish", new SqsOperationBinding
        {
            Queues = [new Queue { Name = "price-changes-queue.fifo", FifoQueue = true }],
        })
        .AddMessageBinding("prices-kafka-message", new KafkaMessageBinding { SchemaIdLocation = "payload" });

    [Theory]
    [InlineData("pricesws", "ws")]
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
    public async Task GetAsyncApiDocument_WebSocketsChannelBinding_IsServedUnderWsWithMethod()
    {
        // Act
        using var document = await GetServedDocumentAsync();

        // Assert
        var binding = document.RootElement.GetProperty("channels").GetProperty("pricesws").GetProperty("bindings")
            .EnumerateObject().ShouldHaveSingleItem();
        binding.Name.ShouldBe("ws");
        binding.Value.GetProperty("method").GetString().ShouldBe("GET");
    }

    [Fact]
    public async Task GetAsyncApiDocument_SqsStandardQueue_IsServedWithFifoQueueFalse()
    {
        // Act
        using var document = await GetServedDocumentAsync();

        // Assert
        document.RootElement.GetProperty("channels").GetProperty("pricessqs").GetProperty("bindings")
            .GetProperty("sqs").GetProperty("queue").GetProperty("fifoQueue").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task GetAsyncApiDocument_KafkaMessageBinding_IsServedWithCamelCaseSchemaIdLocation()
    {
        // Act
        using var document = await GetServedDocumentAsync();

        // Assert
        var kafka = document.RootElement.GetProperty("components").GetProperty("messages")
            .GetProperty("priceChangedOverKafka").GetProperty("bindings").GetProperty("kafka");
        kafka.GetProperty("schemaIdLocation").GetString().ShouldBe("payload");
        kafka.TryGetProperty("SchemaIdLocation", out _).ShouldBeFalse();
    }

    private static async Task<JsonDocument> GetServedDocumentAsync()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers();
        builder.Services.AddAsyncApi(DocumentName, Configure);

        await using var app = builder.Build();
        app.MapAsyncApi();
        await app.StartAsync();

        var json = await app.GetTestClient().GetStringAsync(
            AsyncApiGeneratorConstants.DefaultAsyncApiRoute.Replace("{documentName}", DocumentName));
        await app.StopAsync();

        return JsonDocument.Parse(json);
    }
}
