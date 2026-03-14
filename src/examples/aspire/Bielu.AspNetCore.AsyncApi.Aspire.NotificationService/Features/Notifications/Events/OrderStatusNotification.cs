// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Bielu.AspNetCore.AsyncApi.Aspire.NotificationService.Features.Notifications.Events;

/// <summary>
/// Notification sent to clients when an order status changes.
/// </summary>
public class OrderStatusNotification
{
    /// <summary>
    /// The order identifier.
    /// </summary>
    public Guid OrderId { get; set; }

    /// <summary>
    /// The new status of the order.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable notification message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the notification was generated.
    /// </summary>
    public DateTime Timestamp { get; set; }
}
