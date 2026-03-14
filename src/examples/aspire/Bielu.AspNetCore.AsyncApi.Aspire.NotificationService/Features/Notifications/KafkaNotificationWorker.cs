// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Bielu.AspNetCore.AsyncApi.Aspire.NotificationService.Features.Notifications.Diagnostics;
using Bielu.AspNetCore.AsyncApi.Aspire.NotificationService.Features.Notifications.Events;
using Bielu.AspNetCore.AsyncApi.Aspire.NotificationService.Features.Notifications.Hubs;
using Bielu.AspNetCore.AsyncApi.Aspire.ServiceDefaults.Diagnostics;
using Confluent.Kafka;
using Microsoft.AspNetCore.SignalR;

namespace Bielu.AspNetCore.AsyncApi.Aspire.NotificationService.Features.Notifications;

/// <summary>
/// Background worker that consumes events from Kafka topics and pushes
/// real-time notifications to connected SignalR clients.
/// </summary>
public class KafkaNotificationWorker(
    ActivitySourceProvider activitySourceProvider,
    IConsumer<string, string> consumer,
    IHubContext<OrderNotificationHub> orderHub,
    IHubContext<InventoryNotificationHub> inventoryHub,
    NotificationMetrics metrics,
    ILogger<KafkaNotificationWorker> logger) : BackgroundService
{
    private readonly ActivitySource _activitySource = activitySourceProvider.ActivitySource;

    private static readonly string[] s_topics =
    [
        "orders.created",
        "orders.status-changed",
        "inventory.reserved",
        "inventory.stock-level-changed"
    ];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        consumer.Subscribe(s_topics);
        logger.LogInformation("KafkaNotificationWorker subscribed to topics: {Topics}", string.Join(", ", s_topics));

        await Task.Yield(); // Allow startup to continue

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(TimeSpan.FromSeconds(1));
                if (result is null) continue;

                using var activity = _activitySource.StartActivity("ConsumeKafkaMessage", ActivityKind.Consumer);
                activity?.SetTag("messaging.system", "kafka");
                activity?.SetTag("messaging.source", result.Topic);
                activity?.SetTag("messaging.kafka.message_key", result.Message.Key);

                metrics.MessageConsumed(result.Topic);
                logger.LogInformation("Received message from topic {Topic}: {Key}", result.Topic, result.Message.Key);

                switch (result.Topic)
                {
                    case "orders.created":
                    case "orders.status-changed":
                        await HandleOrderEvent(result);
                        break;
                    case "inventory.reserved":
                    case "inventory.stock-level-changed":
                        await HandleInventoryEvent(result);
                        break;
                }
            }
            catch (ConsumeException ex)
            {
                metrics.MessageConsumeFailed();
                logger.LogError(ex, "Error consuming Kafka message");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        consumer.Close();
    }

    private async Task HandleOrderEvent(ConsumeResult<string, string> result)
    {
        using var activity = _activitySource.StartActivity("HandleOrderEvent");

        var notification = new OrderStatusNotification
        {
            OrderId = Guid.TryParse(result.Message.Key, out var id) ? id : Guid.Empty,
            Status = result.Topic == "orders.created" ? "Created" : "StatusChanged",
            Message = $"Order event received from topic '{result.Topic}'",
            Timestamp = DateTime.UtcNow
        };

        if (notification.OrderId == Guid.Empty)
        {
            logger.LogWarning("Failed to parse order ID from Kafka message key: {Key}", result.Message.Key);
        }

        activity?.SetTag("order.id", notification.OrderId.ToString());
        activity?.SetTag("notification.type", "order");

        await orderHub.Clients.All.SendAsync("ReceiveOrderStatusUpdate", notification);

        metrics.OrderNotificationPushed();
        metrics.NotificationPushed();
        logger.LogInformation("Pushed order notification for {OrderId} to SignalR clients", notification.OrderId);
    }

    private async Task HandleInventoryEvent(ConsumeResult<string, string> result)
    {
        using var activity = _activitySource.StartActivity("HandleInventoryEvent");

        var severity = result.Topic == "inventory.stock-level-changed" ? "Info" : "Warning";

        var notification = new InventoryAlertNotification
        {
            ProductId = result.Message.Key ?? "unknown",
            Severity = severity,
            Message = $"Inventory event received from topic '{result.Topic}'",
            Timestamp = DateTime.UtcNow
        };

        activity?.SetTag("product.id", notification.ProductId);
        activity?.SetTag("notification.type", "inventory");

        await inventoryHub.Clients.All.SendAsync("ReceiveInventoryAlert", notification);

        metrics.InventoryNotificationPushed();
        metrics.NotificationPushed();
        logger.LogInformation("Pushed inventory notification for {ProductId} to SignalR clients", notification.ProductId);
    }
}
