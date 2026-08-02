using Confluent.Kafka;

namespace Bielu.AspNetCore.AsyncApi.Scalar.Broker.Kafka;

/// <summary>
/// Per-connection Kafka settings for the broker console bridge.
/// </summary>
public sealed class KafkaConnectionOptions
{
    /// <summary>
    /// Applied to the producer used by <c>POST {path}/publish</c>. Use this for SASL/SSL settings,
    /// client ids, timeouts, and anything else the console should publish with.
    /// </summary>
    public Action<ProducerConfig>? ConfigureProducer { get; set; }

    /// <summary>
    /// Applied to the ephemeral consumer used by <c>GET {path}/tail</c>, after the console's own
    /// defaults.
    /// </summary>
    /// <remarks>
    /// The defaults exist to keep a console from disturbing production traffic: a unique group id
    /// per tail, <see cref="AutoOffsetReset.Latest" />, and auto-commit off. Overriding them —
    /// especially setting a fixed <c>GroupId</c> or re-enabling auto-commit — means opening the
    /// console will move a real consumer group's committed offsets.
    /// </remarks>
    public Action<ConsumerConfig>? ConfigureConsumer { get; set; }
}
