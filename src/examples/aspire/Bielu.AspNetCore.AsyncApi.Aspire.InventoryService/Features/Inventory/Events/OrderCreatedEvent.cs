// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Bielu.AspNetCore.AsyncApi.Aspire.InventoryService.Features.Inventory.Events;

/// <summary>
/// Event consumed when a new order is created (from Order Service).
/// </summary>
public class OrderCreatedEvent
{
    /// <summary>
    /// The order identifier.
    /// </summary>
    public Guid OrderId { get; set; }

    /// <summary>
    /// The product identifier that was ordered.
    /// </summary>
    public string ProductId { get; set; } = string.Empty;

    /// <summary>
    /// The quantity ordered.
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// Timestamp when the event occurred.
    /// </summary>
    public DateTime Timestamp { get; set; }
}
