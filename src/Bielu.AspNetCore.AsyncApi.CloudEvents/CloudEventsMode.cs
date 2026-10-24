namespace Bielu.AspNetCore.AsyncApi.CloudEvents;

/// <summary>How a CloudEvent travels in a message, as the CloudEvents protocol bindings define it.</summary>
public enum CloudEventsMode
{
    /// <summary>
    /// The context attributes travel as protocol headers (<c>ce-id</c>, <c>ce_type</c>, …) and the payload is the
    /// event data, unchanged. The message's headers schema gains the attributes.
    /// </summary>
    Binary,

    /// <summary>
    /// The whole event, attributes and data, is the payload: an <c>application/cloudevents+json</c> envelope
    /// whose <c>data</c> member is the original payload.
    /// </summary>
    Structured,
}

/// <summary>
/// The CloudEvents protocol binding whose header names binary mode uses. Each binding prefixes the attribute names
/// differently.
/// </summary>
public enum CloudEventsProtocolBinding
{
    /// <summary>
    /// Chooses per message from the protocol of the servers its channels use, falling back to <see cref="Http"/>
    /// when there are none or they disagree.
    /// </summary>
    Auto,

    /// <summary>HTTP (and WebSockets, SSE, SignalR): <c>ce-id</c>.</summary>
    Http,

    /// <summary>Kafka: <c>ce_id</c>.</summary>
    Kafka,

    /// <summary>AMQP: <c>cloudEvents:id</c> in the application properties.</summary>
    Amqp,

    /// <summary>MQTT 5: <c>id</c> as a user property, with no prefix.</summary>
    Mqtt,
}
