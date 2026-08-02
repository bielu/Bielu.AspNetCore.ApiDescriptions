using System.Runtime.CompilerServices;
using System.Text;
using Confluent.Kafka;

namespace Bielu.AspNetCore.AsyncApi.Scalar.Broker.Kafka;

/// <summary>
/// <see cref="IBrokerBridge" /> over <c>Confluent.Kafka</c>: publishes to a topic, and tails one
/// with a throwaway consumer group.
/// </summary>
internal sealed class KafkaBrokerBridge(string bootstrapServers, KafkaConnectionOptions options) : IBrokerBridge
{
    // One producer per connection, built on first publish. Producers are expensive (each owns a
    // broker connection and a background send thread) and are documented as thread-safe, so sharing
    // one across requests is both correct and what librdkafka expects.
    private readonly Lazy<IProducer<string?, string>> _producer = new(() =>
    {
        var config = new ProducerConfig { BootstrapServers = bootstrapServers };
        options.ConfigureProducer?.Invoke(config);
        return new ProducerBuilder<string?, string>(config).Build();
    });

    public async Task<BrokerPublishReceipt> PublishAsync(BrokerPublishRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var message = new Message<string?, string> { Key = request.Key, Value = request.Payload };
        if (request.Headers is { Count: > 0 })
        {
            message.Headers = [];
            foreach (var (name, value) in request.Headers)
            {
                message.Headers.Add(name, Encoding.UTF8.GetBytes(value));
            }
        }

        var result = await _producer.Value.ProduceAsync(request.Channel, message, cancellationToken);

        return new BrokerPublishReceipt(
            result.Topic,
            result.Timestamp.UtcDateTime,
            result.Partition.Value,
            result.Offset.Value);
    }

    public async IAsyncEnumerable<BrokerMessage> TailAsync(
        BrokerTailRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var config = new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            // A group id nobody else uses, so this consumer joins no real group and its offsets are
            // never anyone's business.
            GroupId = $"bielu-scalar-broker-{Guid.NewGuid():N}",
            // Start at the head: a console shows what is happening now, and must not replay a
            // backlog into the browser.
            AutoOffsetReset = AutoOffsetReset.Latest,
            // Never write offsets back. Combined with the throwaway group id this makes tailing
            // observably free of side effects on the cluster.
            EnableAutoCommit = false,
        };
        options.ConfigureConsumer?.Invoke(config);

        using var consumer = new ConsumerBuilder<string?, string>(config).Build();
        consumer.Subscribe(request.Channel);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // Consume blocks the calling thread. Push it to the thread pool so the SSE writer
                // awaiting us keeps its request thread, and poll on a short timeout so cancellation
                // is noticed promptly on an idle topic.
                var result = await Task.Run(
                    () => consumer.Consume(TimeSpan.FromMilliseconds(500)),
                    cancellationToken);

                if (result?.Message is null)
                {
                    continue;
                }

                yield return new BrokerMessage(
                    result.Topic,
                    result.Message.Key,
                    Headers(result.Message.Headers),
                    result.Message.Value,
                    result.Message.Timestamp.UtcDateTime,
                    result.Partition.Value,
                    result.Offset.Value);
            }
        }
        finally
        {
            // Leave the group promptly instead of waiting for the session timeout to expire.
            consumer.Close();
        }
    }

    private static Dictionary<string, string> Headers(Confluent.Kafka.Headers? headers)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (headers is null)
        {
            return result;
        }

        foreach (var header in headers)
        {
            var value = header.GetValueBytes();
            // Kafka headers are arbitrary bytes; the console displays text, so anything that is not
            // UTF-8 is surfaced as a length rather than as mojibake.
            result[header.Key] = value is null
                ? string.Empty
                : TryDecodeUtf8(value, out var text) ? text : $"<{value.Length} bytes>";
        }

        return result;
    }

    private static bool TryDecodeUtf8(byte[] value, out string text)
    {
        try
        {
            text = new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(value);
            return true;
        }
        catch (DecoderFallbackException)
        {
            text = string.Empty;
            return false;
        }
    }

    public ValueTask DisposeAsync()
    {
        if (_producer.IsValueCreated)
        {
            // Block briefly for in-flight sends rather than dropping them on shutdown.
            _producer.Value.Flush(TimeSpan.FromSeconds(5));
            _producer.Value.Dispose();
        }

        return ValueTask.CompletedTask;
    }
}
