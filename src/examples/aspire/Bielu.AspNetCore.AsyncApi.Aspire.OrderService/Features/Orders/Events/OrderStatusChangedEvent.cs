// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Bielu.AspNetCore.AsyncApi.Aspire.OrderService.Features.Orders.Events;

/// <summary>
/// Event published when an order status changes.
/// </summary>
public class OrderStatusChangedEvent
{
    /// <summary>
    /// The order identifier.
    /// </summary>
    public Guid OrderId { get; set; }

    /// <summary>
    /// The previous status of the order.
    /// </summary>
    public string PreviousStatus { get; set; } = string.Empty;

    /// <summary>
    /// The new status of the order.
    /// </summary>
    public string NewStatus { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the status change occurred.
    /// </summary>
    public DateTime Timestamp { get; set; }
}
