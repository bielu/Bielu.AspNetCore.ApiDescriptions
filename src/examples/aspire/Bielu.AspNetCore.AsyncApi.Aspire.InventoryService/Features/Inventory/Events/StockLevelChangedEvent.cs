// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Bielu.AspNetCore.AsyncApi.Aspire.InventoryService.Features.Inventory.Events;

/// <summary>
/// Event published when stock levels change.
/// </summary>
public class StockLevelChangedEvent
{
    /// <summary>
    /// The product identifier.
    /// </summary>
    public string ProductId { get; set; } = string.Empty;

    /// <summary>
    /// Previous available quantity.
    /// </summary>
    public int PreviousQuantity { get; set; }

    /// <summary>
    /// New available quantity.
    /// </summary>
    public int NewQuantity { get; set; }

    /// <summary>
    /// Reason for the stock change.
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the change occurred.
    /// </summary>
    public DateTime Timestamp { get; set; }
}
