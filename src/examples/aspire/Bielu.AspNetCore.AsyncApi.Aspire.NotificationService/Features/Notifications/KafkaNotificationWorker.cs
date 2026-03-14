// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
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
    private readonly IConsumer<string, string> _consumer;
    private readonly IHubContext<OrderNotificationHub> _orderHub;
    private readonly IHubContext<InventoryNotificationHub> _inventoryHub;
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
        ILogger<KafkaNotificationWorker> logger)
    {
        _consumer = consumer;
        _orderHub = orderHub;
        _inventoryHub = inventoryHub;
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
        var notification = new OrderStatusNotification
        {
            OrderId = Guid.TryParse(result.Message.Key, out var id) ? id : Guid.Empty,
            Status = result.Topic == "orders.created" ? "Created" : "StatusChanged",
            Message = $"Order event received from topic '{result.Topic}'",
            Timestamp = DateTime.UtcNow
        };

        await _orderHub.Clients.All.SendAsync("ReceiveOrderStatusUpdate", notification);
        _logger.LogInformation("Pushed order notification for {OrderId} to SignalR clients", notification.OrderId);
    }

    private async Task HandleInventoryEvent(ConsumeResult<string, string> result)
    {
        var severity = result.Topic == "inventory.stock-level-changed" ? "Info" : "Warning";

        var notification = new InventoryAlertNotification
        {
            ProductId = result.Message.Key ?? "unknown",
            Severity = severity,
            Message = $"Inventory event received from topic '{result.Topic}'",
            Timestamp = DateTime.UtcNow
        };

        await _inventoryHub.Clients.All.SendAsync("ReceiveInventoryAlert", notification);
        _logger.LogInformation("Pushed inventory notification for {ProductId} to SignalR clients", notification.ProductId);
    }
}
