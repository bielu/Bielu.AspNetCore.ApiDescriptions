// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bielu.AspNetCore.AsyncApi.Aspire.InventoryService.Features.Inventory.Events;
using Bielu.AspNetCore.AsyncApi.Aspire.InventoryService.Features.Inventory.Models;

namespace Bielu.AspNetCore.AsyncApi.Aspire.InventoryService.Features.Inventory.Services;

/// <summary>
/// Service interface for inventory management operations.
/// </summary>
public interface IInventoryManagementService
{
    /// <summary>
    /// Get all inventory items.
    /// </summary>
    Task<IEnumerable<InventoryItem>> GetAllAsync();

    /// <summary>
    /// Get inventory for a specific product.
    /// Returns null if not found.
    /// </summary>
    Task<InventoryItem?> GetByProductIdAsync(string productId);

    /// <summary>
    /// Reserve inventory for an order event.
    /// </summary>
    Task<InventoryReservedEvent> ReserveInventoryAsync(OrderCreatedEvent orderEvent);

    /// <summary>
    /// Restock a product by adding additional quantity.
    /// Returns null if the product was not found.
    /// </summary>
    Task<InventoryItem?> RestockAsync(string productId, int additionalQuantity);
}
