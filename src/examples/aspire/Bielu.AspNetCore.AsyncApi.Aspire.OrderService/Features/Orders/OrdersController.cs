// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Bielu.AspNetCore.AsyncApi.Aspire.OrderService.Features.Orders.Data;
using Bielu.AspNetCore.AsyncApi.Aspire.OrderService.Features.Orders.Events;
using Bielu.AspNetCore.AsyncApi.Aspire.OrderService.Features.Orders.Models;
using Bielu.AspNetCore.AsyncApi.Attributes;
using Bielu.AspNetCore.AsyncApi.Attributes.Attributes;
using Confluent.Kafka;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace Bielu.AspNetCore.AsyncApi.Aspire.OrderService.Features.Orders;

/// <summary>
/// Controller for managing orders. Publishes events to Kafka when orders are created or updated.
/// Data is persisted to PostgreSQL via EF Core and cached in Valkey.
/// </summary>
[AsyncApi]
[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private const string OrderCreatedTopic = "orders.created";
    private const string OrderStatusChangedTopic = "orders.status-changed";

    private readonly IProducer<string, string> _kafkaProducer;
    private readonly OrderDbContext _dbContext;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(
        IProducer<string, string> kafkaProducer,
        OrderDbContext dbContext,
        IConnectionMultiplexer redis,
        ILogger<OrdersController> logger)
    {
        _kafkaProducer = kafkaProducer;
        _dbContext = dbContext;
        _redis = redis;
        _logger = logger;
    }

    /// <summary>
    /// Get all orders from PostgreSQL.
    /// </summary>
    [HttpGet]
    public async Task<IEnumerable<Order>> GetAll()
    {
        return await _dbContext.Orders.OrderByDescending(o => o.CreatedAt).ToListAsync();
    }

    /// <summary>
    /// Get a specific order by ID. Uses Valkey cache with PostgreSQL fallback.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Order>> GetById(Guid id)
    {
        var db = _redis.GetDatabase();
        var cacheKey = $"order:{id}";

        // Try cache first
        var cached = await db.StringGetAsync(cacheKey);
        if (cached.HasValue)
        {
            var cachedOrder = JsonSerializer.Deserialize<Order>(cached!);
            if (cachedOrder is not null) return cachedOrder;
        }

        // Fallback to PostgreSQL via EF Core
        var order = await _dbContext.Orders.FindAsync(id);
        if (order is null) return NotFound();

        // Cache for 5 minutes
        await db.StringSetAsync(cacheKey, JsonSerializer.Serialize(order), TimeSpan.FromMinutes(5));
        return order;
    }

    /// <summary>
    /// Create a new order. Persists to PostgreSQL via EF Core, caches in Valkey,
    /// and publishes an OrderCreatedEvent to Kafka.
    /// </summary>
    [Channel(OrderCreatedTopic, Servers = ["kafka"])]
    [PublishOperation(typeof(OrderCreatedEvent), "OrderCreated", BindingsRef = "kafkaOrderChannel")]
    [HttpPost]
    public async Task<ActionResult<Order>> Create([FromBody] Order order)
    {
        order.Id = Guid.NewGuid();
        order.Status = "Pending";
        order.CreatedAt = DateTime.UtcNow;

        // Persist to PostgreSQL via EF Core
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        // Cache in Valkey
        var db = _redis.GetDatabase();
        await db.StringSetAsync($"order:{order.Id}", JsonSerializer.Serialize(order), TimeSpan.FromMinutes(5));

        // Publish event to Kafka
        var orderCreatedEvent = new OrderCreatedEvent
        {
            OrderId = order.Id,
            ProductId = order.ProductId,
            Quantity = order.Quantity,
            Timestamp = DateTime.UtcNow
        };

        await _kafkaProducer.ProduceAsync(OrderCreatedTopic,
            new Message<string, string>
            {
                Key = order.Id.ToString(),
                Value = JsonSerializer.Serialize(orderCreatedEvent)
            });

        _logger.LogInformation("Order {OrderId} created and published to {Topic}", order.Id, OrderCreatedTopic);

        return CreatedAtAction(nameof(GetById), new { id = order.Id }, order);
    }

    /// <summary>
    /// Update the status of an order. Publishes an OrderStatusChangedEvent to Kafka.
    /// </summary>
    [Channel(OrderStatusChangedTopic, Servers = ["kafka"])]
    [PublishOperation(typeof(OrderStatusChangedEvent), "OrderStatusChanged", BindingsRef = "kafkaOrderChannel")]
    [HttpPut("{id:guid}/status")]
    public async Task<ActionResult> UpdateStatus(Guid id, [FromBody] string newStatus)
    {
        // Update in PostgreSQL via EF Core
        var order = await _dbContext.Orders.FindAsync(id);
        if (order is null) return NotFound();

        var previousStatus = order.Status;
        order.Status = newStatus;
        await _dbContext.SaveChangesAsync();

        // Invalidate cache
        var db = _redis.GetDatabase();
        await db.KeyDeleteAsync($"order:{id}");

        // Publish event to Kafka
        var statusChangedEvent = new OrderStatusChangedEvent
        {
            OrderId = id,
            PreviousStatus = previousStatus,
            NewStatus = newStatus,
            Timestamp = DateTime.UtcNow
        };

        await _kafkaProducer.ProduceAsync(OrderStatusChangedTopic,
            new Message<string, string>
            {
                Key = id.ToString(),
                Value = JsonSerializer.Serialize(statusChangedEvent)
            });

        _logger.LogInformation("Order {OrderId} status changed to {Status} and published to {Topic}",
            id, newStatus, OrderStatusChangedTopic);

        return Ok(new { id, status = newStatus });
    }
}
