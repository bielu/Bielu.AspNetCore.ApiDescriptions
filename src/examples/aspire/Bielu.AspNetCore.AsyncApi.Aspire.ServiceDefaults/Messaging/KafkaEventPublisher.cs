// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;

namespace Bielu.AspNetCore.AsyncApi.Aspire.ServiceDefaults.Messaging;

/// <summary>
/// Kafka-backed implementation of <see cref="IEventPublisher"/>.
/// Handles serialization, tracing, and structured logging for all event publishing.
/// </summary>
public class KafkaEventPublisher : IEventPublisher
{
    private static readonly ActivitySource s_activitySource = new("MiniShop.Messaging");

    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaEventPublisher> _logger;

    public KafkaEventPublisher(IProducer<string, string> producer, ILogger<KafkaEventPublisher> logger)
    {
        _producer = producer;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task PublishAsync<TEvent>(string topic, string key, TEvent @event, CancellationToken cancellationToken = default)
    {
        using var activity = s_activitySource.StartActivity($"Publish {topic}", ActivityKind.Producer);
        activity?.SetTag("messaging.system", "kafka");
        activity?.SetTag("messaging.destination", topic);
        activity?.SetTag("messaging.destination_kind", "topic");
        activity?.SetTag("messaging.kafka.message_key", key);

        var payload = JsonSerializer.Serialize(@event);

        await _producer.ProduceAsync(topic,
            new Message<string, string>
            {
                Key = key,
                Value = payload
            }, cancellationToken);

        _logger.LogInformation("Published {EventType} to {Topic} with key {Key}",
            typeof(TEvent).Name, topic, key);
    }
}
