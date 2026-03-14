// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Bielu.AspNetCore.AsyncApi.Aspire.OrderService.Features.OrderTracking.Events;

/// <summary>
/// Event sent over WebSocket when order tracking information is updated.
/// </summary>
public class OrderTrackingUpdate
{
    /// <summary>
    /// The order identifier being tracked.
    /// </summary>
    public Guid OrderId { get; set; }

    /// <summary>
    /// Current tracking status (e.g. "Processing", "Shipped", "Delivered").
    /// </summary>
    public string TrackingStatus { get; set; } = string.Empty;

    /// <summary>
    /// Current location or step in the fulfillment pipeline.
    /// </summary>
    public string CurrentStep { get; set; } = string.Empty;

    /// <summary>
    /// Estimated delivery time, if available.
    /// </summary>
    public DateTime? EstimatedDelivery { get; set; }

    /// <summary>
    /// Timestamp of this tracking update.
    /// </summary>
    public DateTime Timestamp { get; set; }
}
