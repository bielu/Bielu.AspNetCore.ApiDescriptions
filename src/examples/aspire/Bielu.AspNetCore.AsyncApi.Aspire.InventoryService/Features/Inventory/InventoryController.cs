// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Bielu.AspNetCore.AsyncApi.Aspire.InventoryService.Features.Inventory.Data;
using Bielu.AspNetCore.AsyncApi.Aspire.InventoryService.Features.Inventory.Events;
using Bielu.AspNetCore.AsyncApi.Aspire.InventoryService.Features.Inventory.Models;
using Bielu.AspNetCore.AsyncApi.Attributes;
using Bielu.AspNetCore.AsyncApi.Attributes.Attributes;
using Confluent.Kafka;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bielu.AspNetCore.AsyncApi.Aspire.InventoryService.Features.Inventory;

/// <summary>
/// Controller for managing inventory. Subscribes to order events and publishes inventory events via Kafka.
/// Data is persisted to PostgreSQL via EF Core. Document storage uses Apache Ozone.
/// </summary>
[AsyncApi]
[ApiController]
[Route("api/[controller]")]
public class InventoryController : ControllerBase
{
    private const string InventoryReservedTopic = "inventory.reserved";
    private const string StockLevelChangedTopic = "inventory.stock-level-changed";

    private readonly IProducer<string, string> _kafkaProducer;
    private readonly InventoryDbContext _dbContext;
    private readonly ILogger<InventoryController> _logger;

    public InventoryController(
        IProducer<string, string> kafkaProducer,
        InventoryDbContext dbContext,
        ILogger<InventoryController> logger)
    {
        _kafkaProducer = kafkaProducer;
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Get all inventory items from PostgreSQL.
    /// </summary>
    [HttpGet]
    public async Task<IEnumerable<InventoryItem>> GetAll()
    {
        return await _dbContext.InventoryItems.ToListAsync();
    }

    /// <summary>
    /// Get inventory for a specific product from PostgreSQL.
    /// </summary>
    [HttpGet("{productId}")]
    public async Task<ActionResult<InventoryItem>> GetByProductId(string productId)
    {
        var item = await _dbContext.InventoryItems.FindAsync(productId);
        if (item is null) return NotFound();
        return item;
    }

    /// <summary>
    /// Handle an incoming order-created event by reserving inventory.
    /// Updates PostgreSQL via EF Core and publishes an InventoryReservedEvent to Kafka.
    /// </summary>
    [Channel(InventoryReservedTopic, Servers = ["kafka"])]
    [SubscribeOperation(typeof(OrderCreatedEvent), "OrderCreated", BindingsRef = "kafkaInventoryChannel")]
    [PublishOperation(typeof(InventoryReservedEvent), "InventoryReserved", BindingsRef = "kafkaInventoryChannel")]
    [HttpPost("reserve")]
    public async Task<ActionResult<InventoryReservedEvent>> ReserveInventory([FromBody] OrderCreatedEvent orderEvent)
    {
        var reservedEvent = new InventoryReservedEvent
        {
            OrderId = orderEvent.OrderId,
            ProductId = orderEvent.ProductId,
            Timestamp = DateTime.UtcNow
        };

        var item = await _dbContext.InventoryItems.FindAsync(orderEvent.ProductId);
        if (item is not null && item.QuantityAvailable >= orderEvent.Quantity)
        {
            item.QuantityAvailable -= orderEvent.Quantity;
            item.QuantityReserved += orderEvent.Quantity;
            await _dbContext.SaveChangesAsync();

            reservedEvent.QuantityReserved = orderEvent.Quantity;
            reservedEvent.Success = true;
            _logger.LogInformation("Inventory reserved for order {OrderId}: {Quantity} of {ProductId}",
                orderEvent.OrderId, orderEvent.Quantity, orderEvent.ProductId);
        }
        else
        {
            reservedEvent.Success = false;
            _logger.LogWarning("Insufficient inventory for order {OrderId}: requested {Quantity} of {ProductId}",
                orderEvent.OrderId, orderEvent.Quantity, orderEvent.ProductId);
        }

        // Publish event to Kafka
        await _kafkaProducer.ProduceAsync(InventoryReservedTopic,
            new Message<string, string>
            {
                Key = orderEvent.OrderId.ToString(),
                Value = JsonSerializer.Serialize(reservedEvent)
            });

        _logger.LogInformation("Published InventoryReservedEvent to {Topic}", InventoryReservedTopic);
        return Ok(reservedEvent);
    }

    /// <summary>
    /// Restock a product. Updates PostgreSQL via EF Core and publishes a StockLevelChangedEvent to Kafka.
    /// </summary>
    [Channel(StockLevelChangedTopic, Servers = ["kafka"])]
    [PublishOperation(typeof(StockLevelChangedEvent), "StockLevelChanged", BindingsRef = "kafkaInventoryChannel")]
    [HttpPost("{productId}/restock")]
    public async Task<ActionResult> Restock(string productId, [FromBody] int additionalQuantity)
    {
        var item = await _dbContext.InventoryItems.FindAsync(productId);
        if (item is null) return NotFound();

        var previousQuantity = item.QuantityAvailable;
        item.QuantityAvailable += additionalQuantity;
        await _dbContext.SaveChangesAsync();

        // Publish event to Kafka
        var stockChangedEvent = new StockLevelChangedEvent
        {
            ProductId = productId,
            PreviousQuantity = previousQuantity,
            NewQuantity = item.QuantityAvailable,
            Reason = "Manual restock",
            Timestamp = DateTime.UtcNow
        };

        await _kafkaProducer.ProduceAsync(StockLevelChangedTopic,
            new Message<string, string>
            {
                Key = productId,
                Value = JsonSerializer.Serialize(stockChangedEvent)
            });

        _logger.LogInformation("Restocked {ProductId}: {Previous} -> {New}, published to {Topic}",
            productId, previousQuantity, item.QuantityAvailable, StockLevelChangedTopic);

        return Ok(item);
    }
}
