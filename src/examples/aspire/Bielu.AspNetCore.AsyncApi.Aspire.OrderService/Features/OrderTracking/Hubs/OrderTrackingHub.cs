// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bielu.AspNetCore.AsyncApi.Aspire.OrderService.Features.OrderTracking.Events;
using Bielu.AspNetCore.AsyncApi.Attributes;
using Bielu.AspNetCore.AsyncApi.Attributes.Attributes;
using Microsoft.AspNetCore.SignalR;

namespace Bielu.AspNetCore.AsyncApi.Aspire.OrderService.Features.OrderTracking.Hubs;

/// <summary>
/// SignalR hub providing real-time order tracking updates via WebSocket.
/// Clients subscribe to specific orders and receive live location/status updates.
/// </summary>
[AsyncApi]
[Channel("order-tracking", Servers = ["websocket"])]
public class OrderTrackingHub(ILogger<OrderTrackingHub> logger) : Hub
{
    /// <summary>
    /// Publishes a real-time order tracking update to subscribed clients via WebSocket.
    /// </summary>
    [PublishOperation(typeof(OrderTrackingUpdate), "OrderTrackingUpdate",
        BindingsRef = "wsOrderTrackingChannel",
        Summary = "Receive real-time order tracking updates via WebSocket")]
    public async Task SendTrackingUpdate(OrderTrackingUpdate update)
    {
        logger.LogInformation("Broadcasting tracking update for order {OrderId}: {Status} at {Step}",
            update.OrderId, update.TrackingStatus, update.CurrentStep);

        await Clients.Group($"track-{update.OrderId}").SendAsync("ReceiveTrackingUpdate", update);
    }

    /// <summary>
    /// Subscribe to tracking updates for a specific order.
    /// </summary>
    [SubscribeOperation(typeof(string), "SubscribeToOrderTracking",
        BindingsRef = "wsOrderTrackingChannel",
        Summary = "Subscribe to real-time tracking updates for a specific order")]
    public async Task TrackOrder(string orderId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"track-{orderId}");
        logger.LogInformation("Client {ConnectionId} started tracking order {OrderId}",
            Context.ConnectionId, orderId);
    }

    /// <summary>
    /// Unsubscribe from tracking updates for a specific order.
    /// </summary>
    public async Task UntrackOrder(string orderId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"track-{orderId}");
        logger.LogInformation("Client {ConnectionId} stopped tracking order {OrderId}",
            Context.ConnectionId, orderId);
    }

    public override async Task OnConnectedAsync()
    {
        logger.LogInformation("Client {ConnectionId} connected to OrderTrackingHub",
            Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        logger.LogInformation("Client {ConnectionId} disconnected from OrderTrackingHub",
            Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
