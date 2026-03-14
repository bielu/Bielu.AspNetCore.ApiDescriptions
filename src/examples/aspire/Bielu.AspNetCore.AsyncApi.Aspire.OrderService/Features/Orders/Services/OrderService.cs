// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text.Json;
using Bielu.AspNetCore.AsyncApi.Aspire.OrderService.Features.Orders.Data;
using Bielu.AspNetCore.AsyncApi.Aspire.OrderService.Features.Orders.Diagnostics;
using Bielu.AspNetCore.AsyncApi.Aspire.OrderService.Features.Orders.Events;
using Bielu.AspNetCore.AsyncApi.Aspire.OrderService.Features.Orders.Models;
using Bielu.AspNetCore.AsyncApi.Aspire.ServiceDefaults.Diagnostics;
using Bielu.AspNetCore.AsyncApi.Aspire.ServiceDefaults.Messaging;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace Bielu.AspNetCore.AsyncApi.Aspire.OrderService.Features.Orders.Services;

/// <summary>
/// Service handling order business logic, persistence, caching, and event publishing.
/// </summary>
public class OrderService(
    ActivitySourceProvider activitySourceProvider,
    IEventPublisher eventPublisher,
    OrderDbContext dbContext,
    IConnectionMultiplexer redis,
    OrderMetrics metrics,
    ILogger<OrderService> logger) : IOrderService
{
    private const string OrderCreatedTopic = "orders.created";
    private const string OrderStatusChangedTopic = "orders.status-changed";

    private readonly ActivitySource _activitySource = activitySourceProvider.ActivitySource;

    /// <inheritdoc />
    public async Task<IEnumerable<Order>> GetAllAsync()
    {
        using var activity = _activitySource.StartActivity("GetAllOrders");
        return await dbContext.Orders.OrderByDescending(o => o.CreatedAt).ToListAsync();
    }

    /// <inheritdoc />
    public async Task<Order?> GetByIdAsync(Guid id)
    {
        using var activity = _activitySource.StartActivity("GetOrderById");
        activity?.SetTag("order.id", id.ToString());

        var db = redis.GetDatabase();
        var cacheKey = $"order:{id}";

        // Try cache first
        var cached = await db.StringGetAsync(cacheKey);
        if (cached.HasValue)
        {
            activity?.SetTag("cache.hit", true);
            try
            {
                var cachedOrder = JsonSerializer.Deserialize<Order>(cached.ToString());
                if (cachedOrder is not null) return cachedOrder;
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Failed to deserialize cached order {OrderId}, falling back to database", id);
            }
        }

        activity?.SetTag("cache.hit", false);

        // Fallback to PostgreSQL via EF Core
        var order = await dbContext.Orders.FindAsync(id);
        if (order is null) return null;

        // Cache for 5 minutes
        await db.StringSetAsync(cacheKey, JsonSerializer.Serialize(order), TimeSpan.FromMinutes(5));
        return order;
    }

    /// <inheritdoc />
    public async Task<Order> CreateAsync(Order order)
    {
        using var activity = _activitySource.StartActivity("CreateOrder");
        var now = DateTime.UtcNow;

        order.Id = Guid.NewGuid();
        order.Status = "Pending";
        order.CreatedAt = now;

        activity?.SetTag("order.id", order.Id.ToString());
        activity?.SetTag("order.product_id", order.ProductId);

        // Persist to PostgreSQL via EF Core
        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();

        // Cache in Valkey
        var db = redis.GetDatabase();
        await db.StringSetAsync($"order:{order.Id}", JsonSerializer.Serialize(order), TimeSpan.FromMinutes(5));

        // Publish event to Kafka
        var orderCreatedEvent = new OrderCreatedEvent
        {
            OrderId = order.Id,
            ProductId = order.ProductId,
            Quantity = order.Quantity,
            Timestamp = now
        };

        await eventPublisher.PublishAsync(OrderCreatedTopic, order.Id.ToString(), orderCreatedEvent);
        metrics.OrderCreated();

        logger.LogInformation("Order {OrderId} created and published to {Topic}", order.Id, OrderCreatedTopic);

        return order;
    }

    /// <inheritdoc />
    public async Task<Order?> UpdateStatusAsync(Guid id, string newStatus)
    {
        using var activity = _activitySource.StartActivity("UpdateOrderStatus");
        var now = DateTime.UtcNow;
        activity?.SetTag("order.id", id.ToString());
        activity?.SetTag("order.new_status", newStatus);

        var order = await dbContext.Orders.FindAsync(id);
        if (order is null)
        {
            metrics.StatusUpdateFailed();
            return null;
        }

        var previousStatus = order.Status;
        order.Status = newStatus;
        await dbContext.SaveChangesAsync();

        activity?.SetTag("order.previous_status", previousStatus);

        // Invalidate cache
        var db = redis.GetDatabase();
        await db.KeyDeleteAsync($"order:{id}");

        // Publish event to Kafka
        var statusChangedEvent = new OrderStatusChangedEvent
        {
            OrderId = id,
            PreviousStatus = previousStatus,
            NewStatus = newStatus,
            Timestamp = now
        };

        await eventPublisher.PublishAsync(OrderStatusChangedTopic, id.ToString(), statusChangedEvent);
        metrics.StatusUpdated();

        logger.LogInformation("Order {OrderId} status changed from {PreviousStatus} to {NewStatus}",
            id, previousStatus, newStatus);

        return order;
    }
}
