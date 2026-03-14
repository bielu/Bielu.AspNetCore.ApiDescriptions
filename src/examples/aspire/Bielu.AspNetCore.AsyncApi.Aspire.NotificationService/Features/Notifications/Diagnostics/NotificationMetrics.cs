// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.Metrics;

namespace Bielu.AspNetCore.AsyncApi.Aspire.NotificationService.Features.Notifications.Diagnostics;

/// <summary>
/// OpenTelemetry-compatible metrics for the Notification Service.
/// </summary>
public sealed class NotificationMetrics
{
    public const string MeterName = "MiniShop.NotificationService";

    private readonly Counter<long> _messagesConsumed;
    private readonly Counter<long> _messagesConsumedFailed;
    private readonly Counter<long> _notificationsPushed;
    private readonly Counter<long> _orderNotifications;
    private readonly Counter<long> _inventoryNotifications;

    public NotificationMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _messagesConsumed = meter.CreateCounter<long>(
            "notifications.messages_consumed",
            unit: "{message}",
            description: "Total number of Kafka messages consumed");

        _messagesConsumedFailed = meter.CreateCounter<long>(
            "notifications.messages_consume_failed",
            unit: "{message}",
            description: "Total number of Kafka message consumption failures");

        _notificationsPushed = meter.CreateCounter<long>(
            "notifications.pushed",
            unit: "{notification}",
            description: "Total number of notifications pushed to SignalR clients");

        _orderNotifications = meter.CreateCounter<long>(
            "notifications.order_pushed",
            unit: "{notification}",
            description: "Total number of order notifications pushed");

        _inventoryNotifications = meter.CreateCounter<long>(
            "notifications.inventory_pushed",
            unit: "{notification}",
            description: "Total number of inventory notifications pushed");
    }

    public void MessageConsumed(string topic) => _messagesConsumed.Add(1, new KeyValuePair<string, object?>("topic", topic));
    public void MessageConsumeFailed() => _messagesConsumedFailed.Add(1);
    public void NotificationPushed() => _notificationsPushed.Add(1);
    public void OrderNotificationPushed() => _orderNotifications.Add(1);
    public void InventoryNotificationPushed() => _inventoryNotifications.Add(1);
}
