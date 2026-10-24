using System.Text;
using Bielu.AspNetCore.AsyncApi.Scalar.Aspire.Tests.Fixtures;
using Bielu.AspNetCore.AsyncApi.Scalar.Broker;
using Bielu.AspNetCore.AsyncApi.Scalar.Broker.Kafka;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Scalar.Aspire.Tests.Integration;

/// <summary>
/// Drives the Kafka broker bridge against a real broker started by Aspire. The bridge is created through the
/// public <see cref="KafkaBrokerBridgeOptionsExtensions.AddKafkaConnection"/> registration, exactly as an app
/// would, and runs in the test process.
/// </summary>
public sealed class KafkaBrokerBridgeTests(KafkaFixture kafka) : IClassFixture<KafkaFixture>
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromMinutes(1);

    private (IBrokerBridge Bridge, IServiceProvider Services) CreateBridge(Action<KafkaConnectionOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScalarBrokerBridge(options => options.AddKafkaConnection("orders", kafka.BootstrapServers, configure));
        var provider = services.BuildServiceProvider();
        var registration = provider.GetRequiredService<ScalarBrokerBridgeOptions>().Connections.ShouldHaveSingleItem();
        return (registration.BridgeFactory(provider), provider);
    }

    private static string NewTopic() => $"bridge-tests-{Guid.NewGuid():N}";

    private static async Task<BrokerMessage> FirstAsync(IAsyncEnumerable<BrokerMessage> messages)
    {
        await foreach (var message in messages)
        {
            return message;
        }

        throw new InvalidOperationException("The tail ended without yielding a message.");
    }

    /// <summary>
    /// The tail starts at the head of the topic, so it only sees what is published after its consumer has been
    /// assigned partitions. Keep publishing until the tail yields, instead of racing a single message.
    /// </summary>
    private static async Task<(BrokerMessage Message, BrokerPublishReceipt Receipt)> PublishUntilTailedAsync(
        IBrokerBridge bridge, string topic, Func<CancellationToken, Task<BrokerPublishReceipt?>> publish, CancellationToken cancellationToken)
    {
        // Subscribing to a topic that does not exist yet is an error, so create it with a first message.
        await bridge.PublishAsync(new BrokerPublishRequest(topic, "warm-up"), cancellationToken);

        using var tailCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var tailed = FirstAsync(bridge.TailAsync(new BrokerTailRequest(topic), tailCancellation.Token));

        BrokerPublishReceipt? receipt = null;
        while (!tailed.IsCompleted)
        {
            receipt = await publish(cancellationToken) ?? receipt;
            await Task.WhenAny(tailed, Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken));
        }

        var message = await tailed;
        return (message, receipt!);
    }

    [DockerFact]
    public async Task PublishAsync_ThenTail_RoundTripsKeyHeadersAndPayload()
    {
        // Arrange
        using var timeout = new CancellationTokenSource(TestTimeout);
        var producerConfigured = false;
        var consumerConfigured = false;
        var (bridge, _) = CreateBridge(options =>
        {
            options.ConfigureProducer = config => { producerConfigured = true; config.ClientId = "bridge-tests-producer"; };
            options.ConfigureConsumer = config => { consumerConfigured = true; config.ClientId = "bridge-tests-consumer"; };
        });
        var topic = NewTopic();
        var headers = new Dictionary<string, string> { ["trace-id"] = "abc-123", ["source"] = "tests" };

        await using (bridge)
        {
            // Act
            var (message, receipt) = await PublishUntilTailedAsync(
                bridge,
                topic,
                async ct => await bridge.PublishAsync(new BrokerPublishRequest(topic, "{\"id\":42}", "order-42", headers), ct),
                timeout.Token);

            // Assert
            receipt.Channel.ShouldBe(topic);
            receipt.Partition.ShouldNotBeNull();
            receipt.Offset.ShouldNotBeNull().ShouldBeGreaterThanOrEqualTo(0);

            message.Channel.ShouldBe(topic);
            message.Key.ShouldBe("order-42");
            message.Payload.ShouldBe("{\"id\":42}");
            message.Headers["trace-id"].ShouldBe("abc-123");
            message.Headers["source"].ShouldBe("tests");
            message.Partition.ShouldNotBeNull();
            message.Offset.ShouldNotBeNull().ShouldBeGreaterThanOrEqualTo(0);
        }

        producerConfigured.ShouldBeTrue();
        consumerConfigured.ShouldBeTrue();
    }

    [DockerFact]
    public async Task Tail_NonTextAndRepeatedHeaders_AreMadeDisplayable()
    {
        // Arrange
        using var timeout = new CancellationTokenSource(TestTimeout);
        var (bridge, _) = CreateBridge();
        var topic = NewTopic();
        // Headers the bridge cannot produce itself (it only sends text), so publish them with a raw producer.
        using var producer = new ProducerBuilder<string?, string>(new ProducerConfig { BootstrapServers = kafka.BootstrapServers }).Build();

        await using (bridge)
        {
            // Act
            var (message, _) = await PublishUntilTailedAsync(
                bridge,
                topic,
                async ct =>
                {
                    var headers = new Headers
                    {
                        { "binary", [0xFF, 0xFE, 0x00] },
                        { "empty", null },
                        { "repeated", Encoding.UTF8.GetBytes("first") },
                        { "repeated", Encoding.UTF8.GetBytes("second") },
                    };
                    await producer.ProduceAsync(topic, new Message<string?, string> { Value = "raw", Headers = headers }, ct);
                    return null;
                },
                timeout.Token);

            // Assert
            message.Key.ShouldBeNull();
            message.Payload.ShouldBe("raw");
            message.Headers["binary"].ShouldBe("<3 bytes>");
            message.Headers["empty"].ShouldBe(string.Empty);
            message.Headers["repeated"].ShouldBe("second");
        }
    }

    [DockerFact]
    public async Task TailAsync_IdleTopic_StopsWhenCancelled()
    {
        // Arrange
        using var timeout = new CancellationTokenSource(TestTimeout);
        var (bridge, _) = CreateBridge();
        var topic = NewTopic();

        await using (bridge)
        {
            await bridge.PublishAsync(new BrokerPublishRequest(topic, "warm-up"), timeout.Token);
            using var tailCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var received = new List<BrokerMessage>();

            // Act
            try
            {
                await foreach (var message in bridge.TailAsync(new BrokerTailRequest(topic), tailCancellation.Token))
                {
                    received.Add(message);
                }
            }
            catch (OperationCanceledException)
            {
                // Cancelling mid-poll surfaces as cancellation; ending the loop quietly is equally fine.
            }

            // Assert: the tail starts at the head, so the warm-up message is never replayed.
            received.ShouldBeEmpty();
            timeout.IsCancellationRequested.ShouldBeFalse();
        }
    }

    [Fact]
    public async Task PublishAsync_NullRequest_Throws()
    {
        // Arrange: the bridge is lazy, so no broker is needed to validate arguments.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScalarBrokerBridge(options => options.AddKafkaConnection("orders", "localhost:1"));
        var provider = services.BuildServiceProvider();
        await using var bridge = provider.GetRequiredService<ScalarBrokerBridgeOptions>().Connections[0].BridgeFactory(provider);

        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(() => bridge.PublishAsync(null!, CancellationToken.None));
        await Should.ThrowAsync<ArgumentNullException>(async () =>
        {
            await foreach (var _ in bridge.TailAsync(null!, CancellationToken.None))
            {
            }
        });
    }
}
