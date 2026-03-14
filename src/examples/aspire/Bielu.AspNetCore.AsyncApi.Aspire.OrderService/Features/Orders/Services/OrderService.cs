// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text.Json;
using Bielu.AspNetCore.AsyncApi.Aspire.OrderService.Features.Orders.Data;
using Bielu.AspNetCore.AsyncApi.Aspire.OrderService.Features.Orders.Diagnostics;
using Bielu.AspNetCore.AsyncApi.Aspire.OrderService.Features.Orders.Events;
using Bielu.AspNetCore.AsyncApi.Aspire.OrderService.Features.Orders.Models;
using Bielu.AspNetCore.AsyncApi.Aspire.ServiceDefaults.Messaging;
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

    private static readonly ActivitySource s_activitySource = new("MiniShop.OrderService");

    private readonly IEventPublisher _eventPublisher;
    private readonly OrderDbContext _dbContext;
    private readonly IConnectionMultiplexer _redis;
    private readonly OrderMetrics _metrics;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        IEventPublisher eventPublisher,
        OrderDbContext dbContext,
        IConnectionMultiplexer redis,
        OrderMetrics metrics,
        ILogger<OrderService> logger)
    {
        _eventPublisher = eventPublisher;
        _dbContext = dbContext;
        _redis = redis;
        _metrics = metrics;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Order>> GetAllAsync()
    {
        using var activity = s_activitySource.StartActivity("GetAllOrders");
        return await _dbContext.Orders.OrderByDescending(o => o.CreatedAt).ToListAsync();
    }

    /// <inheritdoc />
    public async Task<Order?> GetByIdAsync(Guid id)
    {
        using var activity = s_activitySource.StartActivity("GetOrderById");
        activity?.SetTag("order.id", id.ToString());

        var db = _redis.GetDatabase();
        var cacheKey = $"order:{id}";

        // Try cache first
        var cached = await db.StringGetAsync(cacheKey);
        if (cached.HasValue)
        {
            activity?.SetTag("cache.hit", true);
            var cachedOrder = JsonSerializer.Deserialize<Order>(cached.ToString());
            if (cachedOrder is not null) return cachedOrder;
        }

        activity?.SetTag("cache.hit", false);

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
        using var activity = s_activitySource.StartActivity("CreateOrder");

        order.Id = Guid.NewGuid();
        order.Status = "Pending";
        order.CreatedAt = DateTime.UtcNow;

        activity?.SetTag("order.id", order.Id.ToString());
        activity?.SetTag("order.product_id", order.ProductId);

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

        await _eventPublisher.PublishAsync(OrderCreatedTopic, order.Id.ToString(), orderCreatedEvent);
        _metrics.OrderCreated();
        _metrics.EventPublished(OrderCreatedTopic);

        _logger.LogInformation("Order {OrderId} created and published to {Topic}", order.Id, OrderCreatedTopic);

        return order;
    }

    /// <inheritdoc />
    public async Task<Order?> UpdateStatusAsync(Guid id, string newStatus)
    {
        using var activity = s_activitySource.StartActivity("UpdateOrderStatus");
        activity?.SetTag("order.id", id.ToString());
        activity?.SetTag("order.new_status", newStatus);

        var order = await _dbContext.Orders.FindAsync(id);
        if (order is null)
        {
            _metrics.StatusUpdateFailed();
            return null;
        }

        var previousStatus = order.Status;
        order.Status = newStatus;
        await _dbContext.SaveChangesAsync();

        activity?.SetTag("order.previous_status", previousStatus);

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

        await _eventPublisher.PublishAsync(OrderStatusChangedTopic, id.ToString(), statusChangedEvent);
        _metrics.StatusUpdated();
        _metrics.EventPublished(OrderStatusChangedTopic);

        _logger.LogInformation("Order {OrderId} status changed from {PreviousStatus} to {NewStatus}",
            id, previousStatus, newStatus);

        return order;
    }
}
