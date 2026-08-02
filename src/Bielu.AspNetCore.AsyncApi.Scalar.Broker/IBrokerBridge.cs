namespace Bielu.AspNetCore.AsyncApi.Scalar.Broker;

/// <summary>
/// The server-side half of the broker console: publishes a message to, and tails messages from, one
/// configured broker connection. Browsers cannot speak Kafka, MQTT or AMQP, so the console reaches
/// the broker through an implementation of this interface rather than directly.
/// </summary>
/// <remarks>
/// One instance serves one connection. Driver packages (for example
/// <c>Bielu.AspNetCore.AsyncApi.Scalar.Broker.Kafka</c>) implement this and register it through
/// <see cref="ScalarBrokerBridgeOptions" />. Implementations are resolved as singletons and must be
/// safe for concurrent use.
/// </remarks>
public interface IBrokerBridge : IAsyncDisposable
{
    /// <summary>
    /// Publishes a single message and returns the broker's acknowledgement.
    /// </summary>
    /// <param name="request">The message to publish.</param>
    /// <param name="cancellationToken">Cancels the publish.</param>
    /// <returns>The broker's acknowledgement metadata.</returns>
    Task<BrokerPublishReceipt> PublishAsync(BrokerPublishRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Streams messages from a channel until the caller stops enumerating or
    /// <paramref name="cancellationToken" /> fires.
    /// </summary>
    /// <remarks>
    /// The subscription is ephemeral and starts at the newest offset: tailing must never disturb a
    /// real consumer group's committed position, and must never replay the backlog into a console.
    /// </remarks>
    /// <param name="request">The channel to tail.</param>
    /// <param name="cancellationToken">Stops the subscription.</param>
    /// <returns>The consumed messages, in arrival order.</returns>
    IAsyncEnumerable<BrokerMessage> TailAsync(BrokerTailRequest request, CancellationToken cancellationToken);
}
