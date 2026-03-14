// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Bielu.AspNetCore.AsyncApi.Aspire.NotificationService.Features.Notifications.Diagnostics;
using Bielu.AspNetCore.AsyncApi.Aspire.NotificationService.Features.Notifications.Events;
using Bielu.AspNetCore.AsyncApi.Aspire.NotificationService.Features.Notifications.Hubs;
using Confluent.Kafka;
using Microsoft.AspNetCore.SignalR;

namespace Bielu.AspNetCore.AsyncApi.Aspire.NotificationService.Features.Notifications;

/// <summary>
/// Background worker that consumes events from Kafka topics and pushes
/// real-time notifications to connected SignalR clients.
/// </summary>
public class KafkaNotificationWorker : BackgroundService
{
    private static readonly ActivitySource s_activitySource = new("MiniShop.NotificationService");

    private readonly IConsumer<string, string> _consumer;
    private readonly IHubContext<OrderNotificationHub> _orderHub;
    private readonly IHubContext<InventoryNotificationHub> _inventoryHub;
    private readonly NotificationMetrics _metrics;
    private readonly ILogger<KafkaNotificationWorker> _logger;

    private static readonly string[] s_topics =
    [
        "orders.created",
        "orders.status-changed",
        "inventory.reserved",
        "inventory.stock-level-changed"
    ];

    public KafkaNotificationWorker(
        IConsumer<string, string> consumer,
        IHubContext<OrderNotificationHub> orderHub,
        IHubContext<InventoryNotificationHub> inventoryHub,
        NotificationMetrics metrics,
        ILogger<KafkaNotificationWorker> logger)
    {
        _consumer = consumer;
        _orderHub = orderHub;
        _inventoryHub = inventoryHub;
        _metrics = metrics;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _consumer.Subscribe(s_topics);
        _logger.LogInformation("KafkaNotificationWorker subscribed to topics: {Topics}", string.Join(", ", s_topics));

        await Task.Yield(); // Allow startup to continue

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = _consumer.Consume(TimeSpan.FromSeconds(1));
                if (result is null) continue;

                using var activity = s_activitySource.StartActivity("ConsumeKafkaMessage", ActivityKind.Consumer);
                activity?.SetTag("messaging.system", "kafka");
                activity?.SetTag("messaging.source", result.Topic);
                activity?.SetTag("messaging.kafka.message_key", result.Message.Key);

                _metrics.MessageConsumed(result.Topic);
                _logger.LogInformation("Received message from topic {Topic}: {Key}", result.Topic, result.Message.Key);

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
                _metrics.MessageConsumeFailed();
                _logger.LogError(ex, "Error consuming Kafka message");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _consumer.Close();
    }

    private async Task HandleOrderEvent(ConsumeResult<string, string> result)
    {
        using var activity = s_activitySource.StartActivity("HandleOrderEvent");

        var notification = new OrderStatusNotification
        {
            OrderId = Guid.TryParse(result.Message.Key, out var id) ? id : Guid.Empty,
            Status = result.Topic == "orders.created" ? "Created" : "StatusChanged",
            Message = $"Order event received from topic '{result.Topic}'",
            Timestamp = DateTime.UtcNow
        };

        if (notification.OrderId == Guid.Empty)
        {
            _logger.LogWarning("Failed to parse order ID from Kafka message key: {Key}", result.Message.Key);
        }

        activity?.SetTag("order.id", notification.OrderId.ToString());
        activity?.SetTag("notification.type", "order");

        await _orderHub.Clients.All.SendAsync("ReceiveOrderStatusUpdate", notification);

        _metrics.OrderNotificationPushed();
        _metrics.NotificationPushed();
        _logger.LogInformation("Pushed order notification for {OrderId} to SignalR clients", notification.OrderId);
    }

    private async Task HandleInventoryEvent(ConsumeResult<string, string> result)
    {
        using var activity = s_activitySource.StartActivity("HandleInventoryEvent");

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

        await _inventoryHub.Clients.All.SendAsync("ReceiveInventoryAlert", notification);

        _metrics.InventoryNotificationPushed();
        _metrics.NotificationPushed();
        _logger.LogInformation("Pushed inventory notification for {ProductId} to SignalR clients", notification.ProductId);
    }
}
