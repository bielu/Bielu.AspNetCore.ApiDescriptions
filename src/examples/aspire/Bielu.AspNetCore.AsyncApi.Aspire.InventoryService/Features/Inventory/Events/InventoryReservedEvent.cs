// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Bielu.AspNetCore.AsyncApi.Aspire.InventoryService.Features.Inventory.Events;

/// <summary>
/// Event published when inventory is reserved for an order.
/// </summary>
public class InventoryReservedEvent
{
    /// <summary>
    /// The order identifier this reservation is for.
    /// </summary>
    public Guid OrderId { get; set; }

    /// <summary>
    /// The product identifier.
    /// </summary>
    public string ProductId { get; set; } = string.Empty;

    /// <summary>
    /// The quantity reserved.
    /// </summary>
    public int QuantityReserved { get; set; }

    /// <summary>
    /// Whether the reservation was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Timestamp when the reservation occurred.
    /// </summary>
    public DateTime Timestamp { get; set; }
}
