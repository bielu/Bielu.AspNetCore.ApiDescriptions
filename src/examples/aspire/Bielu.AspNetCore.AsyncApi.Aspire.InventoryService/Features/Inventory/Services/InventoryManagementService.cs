// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Bielu.AspNetCore.AsyncApi.Aspire.InventoryService.Features.Inventory.Data;
using Bielu.AspNetCore.AsyncApi.Aspire.InventoryService.Features.Inventory.Diagnostics;
using Bielu.AspNetCore.AsyncApi.Aspire.InventoryService.Features.Inventory.Events;
using Bielu.AspNetCore.AsyncApi.Aspire.InventoryService.Features.Inventory.Models;
using Bielu.AspNetCore.AsyncApi.Aspire.ServiceDefaults.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Bielu.AspNetCore.AsyncApi.Aspire.InventoryService.Features.Inventory.Services;

/// <summary>
/// Service handling inventory business logic, persistence, and event publishing.
/// </summary>
public class InventoryManagementService : IInventoryManagementService
{
    private const string InventoryReservedTopic = "inventory.reserved";
    private const string StockLevelChangedTopic = "inventory.stock-level-changed";

    private static readonly ActivitySource s_activitySource = new("MiniShop.InventoryService");

    private readonly IEventPublisher _eventPublisher;
    private readonly InventoryDbContext _dbContext;
    private readonly InventoryMetrics _metrics;
    private readonly ILogger<InventoryManagementService> _logger;

    public InventoryManagementService(
        IEventPublisher eventPublisher,
        InventoryDbContext dbContext,
        InventoryMetrics metrics,
        ILogger<InventoryManagementService> logger)
    {
        _eventPublisher = eventPublisher;
        _dbContext = dbContext;
        _metrics = metrics;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<InventoryItem>> GetAllAsync()
    {
        using var activity = s_activitySource.StartActivity("GetAllInventory");
        return await _dbContext.InventoryItems.ToListAsync();
    }

    /// <inheritdoc />
    public async Task<InventoryItem?> GetByProductIdAsync(string productId)
    {
        using var activity = s_activitySource.StartActivity("GetInventoryByProductId");
        activity?.SetTag("product.id", productId);
        return await _dbContext.InventoryItems.FindAsync(productId);
    }

    /// <inheritdoc />
    public async Task<InventoryReservedEvent> ReserveInventoryAsync(OrderCreatedEvent orderEvent)
    {
        using var activity = s_activitySource.StartActivity("ReserveInventory");
        activity?.SetTag("order.id", orderEvent.OrderId.ToString());
        activity?.SetTag("product.id", orderEvent.ProductId);
        activity?.SetTag("quantity.requested", orderEvent.Quantity);

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

            _metrics.ReservationSucceeded();
            _metrics.InventoryChanged(orderEvent.ProductId);
            activity?.SetTag("reservation.success", true);

            _logger.LogInformation("Inventory reserved for order {OrderId}: {Quantity} of {ProductId}",
                orderEvent.OrderId, orderEvent.Quantity, orderEvent.ProductId);
        }
        else
        {
            reservedEvent.Success = false;

            _metrics.ReservationFailed();
            activity?.SetTag("reservation.success", false);

            _logger.LogWarning("Insufficient inventory for order {OrderId}: requested {Quantity} of {ProductId}",
                orderEvent.OrderId, orderEvent.Quantity, orderEvent.ProductId);
        }

        // Publish event to Kafka
        await _eventPublisher.PublishAsync(InventoryReservedTopic, orderEvent.OrderId.ToString(), reservedEvent);
        _metrics.EventPublished(InventoryReservedTopic);

        return reservedEvent;
    }

    /// <inheritdoc />
    public async Task<InventoryItem?> RestockAsync(string productId, int additionalQuantity)
    {
        using var activity = s_activitySource.StartActivity("RestockInventory");
        activity?.SetTag("product.id", productId);
        activity?.SetTag("quantity.additional", additionalQuantity);

        var item = await _dbContext.InventoryItems.FindAsync(productId);
        if (item is null) return null;

        var previousQuantity = item.QuantityAvailable;
        item.QuantityAvailable += additionalQuantity;
        await _dbContext.SaveChangesAsync();

        activity?.SetTag("quantity.previous", previousQuantity);
        activity?.SetTag("quantity.new", item.QuantityAvailable);

        // Publish event to Kafka
        var stockChangedEvent = new StockLevelChangedEvent
        {
            ProductId = productId,
            PreviousQuantity = previousQuantity,
            NewQuantity = item.QuantityAvailable,
            Reason = "Manual restock",
            Timestamp = DateTime.UtcNow
        };

        await _eventPublisher.PublishAsync(StockLevelChangedTopic, productId, stockChangedEvent);

        _metrics.Restocked();
        _metrics.InventoryChanged(productId);
        _metrics.EventPublished(StockLevelChangedTopic);

        _logger.LogInformation("Restocked {ProductId}: {Previous} -> {New}",
            productId, previousQuantity, item.QuantityAvailable);

        return item;
    }
}
