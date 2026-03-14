// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bielu.AspNetCore.AsyncApi.Aspire.NotificationService.Features.Notifications.Events;
using Bielu.AspNetCore.AsyncApi.Attributes;
using Bielu.AspNetCore.AsyncApi.Attributes.Attributes;
using Microsoft.AspNetCore.SignalR;

namespace Bielu.AspNetCore.AsyncApi.Aspire.NotificationService.Features.Notifications.Hubs;

/// <summary>
/// SignalR hub for real-time inventory alert notifications via WebSocket.
/// Clients connect to receive live updates when inventory levels change.
/// </summary>
[AsyncApi]
[Channel("notifications/inventory-alerts", Servers = ["websocket"])]
public class InventoryNotificationHub(ILogger<InventoryNotificationHub> logger) : Hub
{
    /// <summary>
    /// Sends an inventory alert notification to all connected clients.
    /// </summary>
    [PublishOperation(typeof(InventoryAlertNotification), "InventoryAlertNotification",
        BindingsRef = "wsNotificationChannel",
        Summary = "Receive real-time inventory level alerts via WebSocket")]
    public async Task SendInventoryAlert(InventoryAlertNotification notification)
    {
        logger.LogInformation("Broadcasting inventory alert for product {ProductId}: {Severity}",
            notification.ProductId, notification.Severity);

        await Clients.All.SendAsync("ReceiveInventoryAlert", notification);
    }

    /// <summary>
    /// Subscribe to notifications for a specific product.
    /// </summary>
    [SubscribeOperation(typeof(string), "SubscribeToProduct",
        BindingsRef = "wsNotificationChannel",
        Summary = "Subscribe to inventory alerts for a specific product by ID")]
    public async Task SubscribeToProduct(string productId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"product-{productId}");
        logger.LogInformation("Client {ConnectionId} subscribed to product {ProductId}",
            Context.ConnectionId, productId);
    }

    public override async Task OnConnectedAsync()
    {
        logger.LogInformation("Client {ConnectionId} connected to InventoryNotificationHub",
            Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        logger.LogInformation("Client {ConnectionId} disconnected from InventoryNotificationHub",
            Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
