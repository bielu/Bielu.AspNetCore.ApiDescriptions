// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Bielu.AspNetCore.AsyncApi.Attributes;
using Bielu.AspNetCore.AsyncApi.Attributes.Attributes;
using Microsoft.AspNetCore.Mvc;

namespace Bielu.AspNetCore.AsyncApi.Aspire.InventoryService;

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

/// <summary>
/// Controller for managing inventory. Subscribes to order events and publishes inventory events via Kafka.
/// Data is persisted to PostgreSQL. Document storage uses Apache Ozone.
/// </summary>
[AsyncApi]
[ApiController]
[Route("api/[controller]")]
public class InventoryController : ControllerBase
{
    private const string OrderCreatedTopic = "orders.created";
    private const string InventoryReservedTopic = "inventory.reserved";
    private const string StockLevelChangedTopic = "inventory.stock-level-changed";

    private static readonly List<InventoryItem> s_inventory =
    [
        new() { ProductId = "PROD-001", Name = "Widget A", QuantityAvailable = 100, WarehouseLocation = "WH-1" },
        new() { ProductId = "PROD-002", Name = "Widget B", QuantityAvailable = 50, WarehouseLocation = "WH-1" },
        new() { ProductId = "PROD-003", Name = "Gadget X", QuantityAvailable = 200, WarehouseLocation = "WH-2" }
    ];

    private readonly ILogger<InventoryController> _logger;

    public InventoryController(ILogger<InventoryController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Get all inventory items.
    /// </summary>
    [HttpGet]
    public IEnumerable<InventoryItem> GetAll() => s_inventory;

    /// <summary>
    /// Get inventory for a specific product.
    /// </summary>
    [HttpGet("{productId}")]
    public ActionResult<InventoryItem> GetByProductId(string productId)
    {
        var item = s_inventory.FirstOrDefault(i => i.ProductId == productId);
        if (item is null) return NotFound();
        return item;
    }

    /// <summary>
    /// Handle an incoming order-created event by reserving inventory.
    /// Subscribes to the orders.created Kafka topic and publishes an InventoryReservedEvent.
    /// </summary>
    [Channel(InventoryReservedTopic, Servers = ["kafka"])]
    [SubscribeOperation(typeof(OrderCreatedEvent), "OrderCreated", BindingsRef = "kafkaInventoryChannel")]
    [PublishOperation(typeof(InventoryReservedEvent), "InventoryReserved", BindingsRef = "kafkaInventoryChannel")]
    [HttpPost("reserve")]
    public ActionResult<InventoryReservedEvent> ReserveInventory([FromBody] OrderCreatedEvent orderEvent)
    {
        var item = s_inventory.FirstOrDefault(i => i.ProductId == orderEvent.ProductId);

        var reservedEvent = new InventoryReservedEvent
        {
            OrderId = orderEvent.OrderId,
            ProductId = orderEvent.ProductId,
            Timestamp = DateTime.UtcNow
        };

        if (item is not null && item.QuantityAvailable >= orderEvent.Quantity)
        {
            item.QuantityAvailable -= orderEvent.Quantity;
            item.QuantityReserved += orderEvent.Quantity;
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

        _logger.LogInformation("Publishing InventoryReservedEvent to {Topic}: {Payload}",
            InventoryReservedTopic, JsonSerializer.Serialize(reservedEvent));

        return Ok(reservedEvent);
    }

    /// <summary>
    /// Restock a product. Publishes a StockLevelChangedEvent to the inventory.stock-level-changed Kafka topic.
    /// </summary>
    [Channel(StockLevelChangedTopic, Servers = ["kafka"])]
    [PublishOperation(typeof(StockLevelChangedEvent), "StockLevelChanged", BindingsRef = "kafkaInventoryChannel")]
    [HttpPost("{productId}/restock")]
    public ActionResult Restock(string productId, [FromBody] int additionalQuantity)
    {
        var item = s_inventory.FirstOrDefault(i => i.ProductId == productId);
        if (item is null) return NotFound();

        var previousQuantity = item.QuantityAvailable;
        item.QuantityAvailable += additionalQuantity;

        var stockChangedEvent = new StockLevelChangedEvent
        {
            ProductId = productId,
            PreviousQuantity = previousQuantity,
            NewQuantity = item.QuantityAvailable,
            Reason = "Manual restock",
            Timestamp = DateTime.UtcNow
        };

        _logger.LogInformation("Publishing StockLevelChangedEvent to {Topic}: {Payload}",
            StockLevelChangedTopic, JsonSerializer.Serialize(stockChangedEvent));

        return Ok(item);
    }
}
