// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.Metrics;

namespace Bielu.AspNetCore.AsyncApi.Aspire.ServiceDefaults.Messaging;

/// <summary>
/// Shared OpenTelemetry metrics for event publishing.
/// Registered as a singleton in ServiceDefaults so every <see cref="IEventPublisher"/>
/// implementation can record metrics without each service duplicating the counter.
/// </summary>
public sealed class MessagingMetrics
{
    public const string MeterName = "MiniShop.Messaging";

    private readonly Counter<long> _eventsPublished;

    public MessagingMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _eventsPublished = meter.CreateCounter<long>(
            "messaging.events_published",
            unit: "{event}",
            description: "Total number of events published to the message broker");
    }

    public void EventPublished(string topic) =>
        _eventsPublished.Add(1, new KeyValuePair<string, object?>("topic", topic));
}
