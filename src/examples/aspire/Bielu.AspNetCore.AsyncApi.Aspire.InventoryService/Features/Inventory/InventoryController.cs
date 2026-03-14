// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bielu.AspNetCore.AsyncApi.Aspire.InventoryService.Features.Inventory.Events;
using Bielu.AspNetCore.AsyncApi.Aspire.InventoryService.Features.Inventory.Models;
using Bielu.AspNetCore.AsyncApi.Aspire.InventoryService.Features.Inventory.Services;
using Bielu.AspNetCore.AsyncApi.Attributes;
using Bielu.AspNetCore.AsyncApi.Attributes.Attributes;
using Microsoft.AspNetCore.Mvc;

namespace Bielu.AspNetCore.AsyncApi.Aspire.InventoryService.Features.Inventory;

/// <summary>
/// Controller for managing inventory. Delegates all business logic to <see cref="IInventoryManagementService"/>.
/// </summary>
[AsyncApi]
[ApiController]
[Route("api/[controller]")]
public class InventoryController(IInventoryManagementService inventoryService) : ControllerBase
{
    private const string InventoryReservedTopic = "inventory.reserved";
    private const string StockLevelChangedTopic = "inventory.stock-level-changed";

    /// <summary>
    /// Get all inventory items.
    /// </summary>
    [HttpGet]
    public async Task<IEnumerable<InventoryItem>> GetAll()
    {
        return await inventoryService.GetAllAsync();
    }

    /// <summary>
    /// Get inventory for a specific product.
    /// </summary>
    [HttpGet("{productId}")]
    public async Task<ActionResult<InventoryItem>> GetByProductId(string productId)
    {
        var item = await inventoryService.GetByProductIdAsync(productId);
        if (item is null) return NotFound();
        return item;
    }

    /// <summary>
    /// Handle an incoming order-created event by reserving inventory.
    /// Publishes an InventoryReservedEvent to Kafka.
    /// </summary>
    [Channel(InventoryReservedTopic, Servers = ["kafka"])]
    [SubscribeOperation(typeof(OrderCreatedEvent), "OrderCreated", BindingsRef = "kafkaInventoryChannel")]
    [PublishOperation(typeof(InventoryReservedEvent), "InventoryReserved", BindingsRef = "kafkaInventoryChannel")]
    [HttpPost("reserve")]
    public async Task<ActionResult<InventoryReservedEvent>> ReserveInventory([FromBody] OrderCreatedEvent orderEvent)
    {
        var reservedEvent = await inventoryService.ReserveInventoryAsync(orderEvent);
        return Ok(reservedEvent);
    }

    /// <summary>
    /// Restock a product. Publishes a StockLevelChangedEvent to Kafka.
    /// </summary>
    [Channel(StockLevelChangedTopic, Servers = ["kafka"])]
    [PublishOperation(typeof(StockLevelChangedEvent), "StockLevelChanged", BindingsRef = "kafkaInventoryChannel")]
    [HttpPost("{productId}/restock")]
    public async Task<ActionResult> Restock(string productId, [FromBody] int additionalQuantity)
    {
        var item = await inventoryService.RestockAsync(productId, additionalQuantity);
        if (item is null) return NotFound();
        return Ok(item);
    }
}
