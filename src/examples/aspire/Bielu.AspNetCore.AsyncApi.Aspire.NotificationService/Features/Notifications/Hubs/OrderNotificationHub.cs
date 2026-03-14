// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bielu.AspNetCore.AsyncApi.Aspire.NotificationService.Features.Notifications.Events;
using Bielu.AspNetCore.AsyncApi.Attributes;
using Bielu.AspNetCore.AsyncApi.Attributes.Attributes;
using Microsoft.AspNetCore.SignalR;

namespace Bielu.AspNetCore.AsyncApi.Aspire.NotificationService.Features.Notifications.Hubs;

/// <summary>
/// SignalR hub for real-time order notifications via WebSocket.
/// Clients connect to receive live updates when order statuses change.
/// </summary>
[AsyncApi]
[Channel("notifications/order-status", Servers = ["websocket"])]
public class OrderNotificationHub(ILogger<OrderNotificationHub> logger) : Hub
{
    /// <summary>
    /// Sends an order status notification to all connected clients.
    /// </summary>
    [PublishOperation(typeof(OrderStatusNotification), "OrderStatusNotification",
        BindingsRef = "wsNotificationChannel",
        Summary = "Receive real-time order status updates via WebSocket")]
    public async Task SendOrderStatusUpdate(OrderStatusNotification notification)
    {
        logger.LogInformation("Broadcasting order status notification for order {OrderId}: {Status}",
            notification.OrderId, notification.Status);

        await Clients.All.SendAsync("ReceiveOrderStatusUpdate", notification);
    }

    /// <summary>
    /// Subscribe to notifications for a specific order.
    /// </summary>
    [SubscribeOperation(typeof(string), "SubscribeToOrder",
        BindingsRef = "wsNotificationChannel",
        Summary = "Subscribe to updates for a specific order by ID")]
    public async Task SubscribeToOrder(string orderId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"order-{orderId}");
        logger.LogInformation("Client {ConnectionId} subscribed to order {OrderId}",
            Context.ConnectionId, orderId);
    }

    public override async Task OnConnectedAsync()
    {
        logger.LogInformation("Client {ConnectionId} connected to OrderNotificationHub",
            Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        logger.LogInformation("Client {ConnectionId} disconnected from OrderNotificationHub",
            Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
