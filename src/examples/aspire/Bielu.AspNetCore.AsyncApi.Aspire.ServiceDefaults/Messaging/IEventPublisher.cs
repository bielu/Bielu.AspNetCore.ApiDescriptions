// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Bielu.AspNetCore.AsyncApi.Aspire.ServiceDefaults.Messaging;

/// <summary>
/// Abstraction for publishing events to a message broker.
/// Follows the Interface Segregation Principle — services depend on this
/// rather than directly on Confluent.Kafka's <c>IProducer</c>.
/// </summary>
public interface IEventPublisher
{
    /// <summary>
    /// Publish an event to the specified topic.
    /// </summary>
    /// <typeparam name="TEvent">The event type to serialize.</typeparam>
    /// <param name="topic">The target topic name.</param>
    /// <param name="key">The message key (used for partitioning).</param>
    /// <param name="event">The event payload.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    Task PublishAsync<TEvent>(string topic, string key, TEvent @event, CancellationToken cancellationToken = default);
}
