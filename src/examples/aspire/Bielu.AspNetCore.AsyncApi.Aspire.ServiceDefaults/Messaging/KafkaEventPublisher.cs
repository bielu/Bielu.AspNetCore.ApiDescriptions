// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text.Json;
using Bielu.AspNetCore.AsyncApi.Aspire.ServiceDefaults.Diagnostics;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bielu.AspNetCore.AsyncApi.Aspire.ServiceDefaults.Messaging;

/// <summary>
/// Kafka-backed implementation of <see cref="IEventPublisher"/>.
/// Handles serialization, tracing, metrics, and structured logging for all event publishing.
/// </summary>
public class KafkaEventPublisher(
    [FromKeyedServices(DiagnosticsNames.Messaging)] ActivitySourceProvider activitySourceProvider,
    IProducer<string, string> producer,
    MessagingMetrics messagingMetrics,
    ILogger<KafkaEventPublisher> logger) : IEventPublisher
{
    private readonly ActivitySource _activitySource = activitySourceProvider.ActivitySource;

    /// <inheritdoc />
    public async Task PublishAsync<TEvent>(string topic, string key, TEvent @event, CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity($"Publish {topic}", ActivityKind.Producer);
        activity?.SetTag("messaging.system", "kafka");
        activity?.SetTag("messaging.destination", topic);
        activity?.SetTag("messaging.destination_kind", "topic");
        activity?.SetTag("messaging.kafka.message_key", key);

        var payload = JsonSerializer.Serialize(@event);

        await producer.ProduceAsync(topic,
            new Message<string, string>
            {
                Key = key,
                Value = payload
            }, cancellationToken);

        messagingMetrics.EventPublished(topic);

        logger.LogInformation("Published {EventType} to {Topic} with key {Key}",
            typeof(TEvent).Name, topic.SanitizeLog(), key.SanitizeLog());
    }
}
