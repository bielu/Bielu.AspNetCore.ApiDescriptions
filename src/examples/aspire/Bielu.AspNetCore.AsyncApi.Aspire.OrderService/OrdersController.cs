// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Bielu.AspNetCore.AsyncApi.Attributes;
using Bielu.AspNetCore.AsyncApi.Attributes.Attributes;
using Microsoft.AspNetCore.Mvc;

namespace Bielu.AspNetCore.AsyncApi.Aspire.OrderService;

/// <summary>
/// Represents a customer order.
/// </summary>
public class Order
{
    /// <summary>
    /// Unique order identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Customer identifier.
    /// </summary>
    public string CustomerId { get; set; } = string.Empty;

    /// <summary>
    /// Product identifier.
    /// </summary>
    public string ProductId { get; set; } = string.Empty;

    /// <summary>
    /// Quantity of the product ordered.
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// Total price of the order.
    /// </summary>
    public decimal TotalPrice { get; set; }

    /// <summary>
    /// Current status of the order.
    /// </summary>
    public string Status { get; set; } = "Pending";

    /// <summary>
    /// Timestamp when the order was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Event published when a new order is created.
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

/// <summary>
/// Controller for managing orders. Publishes events to Kafka when orders are created or updated.
/// Data is persisted to PostgreSQL and cached in Valkey.
/// </summary>
[AsyncApi]
[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private const string OrderCreatedTopic = "orders.created";
    private const string OrderStatusChangedTopic = "orders.status-changed";

    private static readonly List<Order> s_orders = [];
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(ILogger<OrdersController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Get all orders.
    /// </summary>
    [HttpGet]
    public IEnumerable<Order> GetAll() => s_orders;

    /// <summary>
    /// Get a specific order by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public ActionResult<Order> GetById(Guid id)
    {
        var order = s_orders.FirstOrDefault(o => o.Id == id);
        if (order is null) return NotFound();
        return order;
    }

    /// <summary>
    /// Create a new order. Publishes an OrderCreatedEvent to the orders.created Kafka topic.
    /// </summary>
    [Channel(OrderCreatedTopic, Servers = ["kafka"])]
    [PublishOperation(typeof(OrderCreatedEvent), "OrderCreated", BindingsRef = "kafkaOrderChannel")]
    [HttpPost]
    public ActionResult<Order> Create([FromBody] Order order)
    {
        order.Id = Guid.NewGuid();
        order.Status = "Pending";
        order.CreatedAt = DateTime.UtcNow;
        s_orders.Add(order);

        var orderCreatedEvent = new OrderCreatedEvent
        {
            OrderId = order.Id,
            ProductId = order.ProductId,
            Quantity = order.Quantity,
            Timestamp = DateTime.UtcNow
        };

        _logger.LogInformation("Order {OrderId} created. Publishing OrderCreatedEvent to {Topic}: {Payload}",
            order.Id, OrderCreatedTopic, JsonSerializer.Serialize(orderCreatedEvent));

        return CreatedAtAction(nameof(GetById), new { id = order.Id }, order);
    }

    /// <summary>
    /// Update the status of an order. Publishes an OrderStatusChangedEvent to the orders.status-changed Kafka topic.
    /// </summary>
    [Channel(OrderStatusChangedTopic, Servers = ["kafka"])]
    [PublishOperation(typeof(OrderStatusChangedEvent), "OrderStatusChanged", BindingsRef = "kafkaOrderChannel")]
    [HttpPut("{id:guid}/status")]
    public ActionResult UpdateStatus(Guid id, [FromBody] string newStatus)
    {
        var order = s_orders.FirstOrDefault(o => o.Id == id);
        if (order is null) return NotFound();

        var previousStatus = order.Status;
        order.Status = newStatus;

        var statusChangedEvent = new OrderStatusChangedEvent
        {
            OrderId = order.Id,
            PreviousStatus = previousStatus,
            NewStatus = newStatus,
            Timestamp = DateTime.UtcNow
        };

        _logger.LogInformation("Order {OrderId} status changed from {Previous} to {New}. Publishing to {Topic}: {Payload}",
            order.Id, previousStatus, newStatus, OrderStatusChangedTopic, JsonSerializer.Serialize(statusChangedEvent));

        return Ok(order);
    }
}
