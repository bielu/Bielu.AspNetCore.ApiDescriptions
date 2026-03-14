// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Bielu.AspNetCore.AsyncApi.Aspire.InventoryService.Features.Inventory.Models;

/// <summary>
/// Represents a product's inventory record.
/// </summary>
public class InventoryItem
{
    /// <summary>
    /// Product identifier.
    /// </summary>
    public string ProductId { get; set; } = string.Empty;

    /// <summary>
    /// Product name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Available quantity in stock.
    /// </summary>
    public int QuantityAvailable { get; set; }

    /// <summary>
    /// Reserved quantity (pending fulfillment).
    /// </summary>
    public int QuantityReserved { get; set; }

    /// <summary>
    /// Warehouse location identifier.
    /// </summary>
    public string WarehouseLocation { get; set; } = string.Empty;
}
