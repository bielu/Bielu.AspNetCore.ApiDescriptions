// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Bielu.AspNetCore.AsyncApi.Aspire.OrderService.Features.Orders.Data;
using Bielu.AspNetCore.AsyncApi.Aspire.OrderService.Features.Orders.Events;
using Bielu.AspNetCore.AsyncApi.Aspire.OrderService.Features.Orders.Models;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace Bielu.AspNetCore.AsyncApi.Aspire.OrderService.Features.Orders.Services;

/// <summary>
/// Service handling order business logic, persistence, caching, and event publishing.
/// </summary>
public class OrderService : IOrderService
{
    private const string OrderCreatedTopic = "orders.created";
    private const string OrderStatusChangedTopic = "orders.status-changed";

    private readonly IProducer<string, string> _kafkaProducer;
    private readonly OrderDbContext _dbContext;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        IProducer<string, string> kafkaProducer,
        OrderDbContext dbContext,
        IConnectionMultiplexer redis,
        ILogger<OrderService> logger)
    {
        _kafkaProducer = kafkaProducer;
        _dbContext = dbContext;
        _redis = redis;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Order>> GetAllAsync()
    {
        return await _dbContext.Orders.OrderByDescending(o => o.CreatedAt).ToListAsync();
    }

    /// <inheritdoc />
    public async Task<Order?> GetByIdAsync(Guid id)
    {
        var db = _redis.GetDatabase();
        var cacheKey = $"order:{id}";

        // Try cache first
        var cached = await db.StringGetAsync(cacheKey);
        if (cached.HasValue)
        {
            var cachedOrder = JsonSerializer.Deserialize<Order>(cached.ToString());
            if (cachedOrder is not null) return cachedOrder;
        }

        // Fallback to PostgreSQL via EF Core
        var order = await _dbContext.Orders.FindAsync(id);
        if (order is null) return null;

        // Cache for 5 minutes
        await db.StringSetAsync(cacheKey, JsonSerializer.Serialize(order), TimeSpan.FromMinutes(5));
        return order;
    }

    /// <inheritdoc />
    public async Task<Order> CreateAsync(Order order)
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

        return order;
    }

    /// <inheritdoc />
    public async Task<Order?> UpdateStatusAsync(Guid id, string newStatus)
    {
        var order = await _dbContext.Orders.FindAsync(id);
        if (order is null) return null;

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

        return order;
    }
}
