namespace Bielu.AspNetCore.AsyncApi.Scalar.Broker;

/// <summary>
/// A message consumed from a broker channel and forwarded to the console's tail stream.
/// </summary>
/// <param name="Channel">The channel (Kafka topic, MQTT topic filter, AMQP queue) the message came from.</param>
/// <param name="Key">The message key, when the protocol has one.</param>
/// <param name="Headers">Protocol headers, stringified for display.</param>
/// <param name="Payload">The message body, decoded as UTF-8 text.</param>
/// <param name="Timestamp">When the broker recorded the message.</param>
/// <param name="Partition">The partition the message came from, when the protocol has partitions.</param>
/// <param name="Offset">The offset within the partition, when the protocol has offsets.</param>
public sealed record BrokerMessage(
    string Channel,
    string? Key,
    IReadOnlyDictionary<string, string> Headers,
    string Payload,
    DateTimeOffset Timestamp,
    int? Partition = null,
    long? Offset = null);

/// <summary>
/// A request to publish one message to a broker channel.
/// </summary>
/// <param name="Channel">The channel to publish to.</param>
/// <param name="Payload">The message body, sent as UTF-8 text.</param>
/// <param name="Key">Optional message key, for protocols that partition by key.</param>
/// <param name="Headers">Optional protocol headers.</param>
public sealed record BrokerPublishRequest(
    string Channel,
    string Payload,
    string? Key = null,
    IReadOnlyDictionary<string, string>? Headers = null);

/// <summary>
/// The broker's acknowledgement of a published message.
/// </summary>
/// <param name="Channel">The channel the message was published to.</param>
/// <param name="Timestamp">When the broker recorded the message.</param>
/// <param name="Partition">The partition the message landed in, when the protocol has partitions.</param>
/// <param name="Offset">The assigned offset, when the protocol has offsets.</param>
public sealed record BrokerPublishReceipt(
    string Channel,
    DateTimeOffset Timestamp,
    int? Partition = null,
    long? Offset = null);

/// <summary>
/// A request to tail a broker channel.
/// </summary>
/// <param name="Channel">The channel to consume from.</param>
public sealed record BrokerTailRequest(string Channel);

/// <summary>
/// A broker connection as the console sees it — never carrying credentials.
/// </summary>
/// <param name="Name">The logical connection name, used to address it on the proxy endpoints.</param>
/// <param name="Protocol">The AsyncAPI protocol identifier (<c>kafka</c>, <c>mqtt</c>, <c>amqp</c>).</param>
/// <param name="Endpoint">A display-only, credential-free description of where the connection points.</param>
public sealed record BrokerConnectionDescriptor(string Name, string Protocol, string Endpoint);
