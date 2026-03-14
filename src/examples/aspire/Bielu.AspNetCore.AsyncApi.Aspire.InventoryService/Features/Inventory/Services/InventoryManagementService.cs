// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Bielu.AspNetCore.AsyncApi.Aspire.InventoryService.Features.Inventory.Data;
using Bielu.AspNetCore.AsyncApi.Aspire.InventoryService.Features.Inventory.Diagnostics;
using Bielu.AspNetCore.AsyncApi.Aspire.InventoryService.Features.Inventory.Events;
using Bielu.AspNetCore.AsyncApi.Aspire.InventoryService.Features.Inventory.Models;
using Bielu.AspNetCore.AsyncApi.Aspire.ServiceDefaults.Diagnostics;
using Bielu.AspNetCore.AsyncApi.Aspire.ServiceDefaults.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Bielu.AspNetCore.AsyncApi.Aspire.InventoryService.Features.Inventory.Services;

/// <summary>
/// Service handling inventory business logic, persistence, and event publishing.
/// </summary>
public class InventoryManagementService(
    ActivitySourceProvider activitySourceProvider,
    IEventPublisher eventPublisher,
    InventoryDbContext dbContext,
    InventoryMetrics metrics,
    ILogger<InventoryManagementService> logger) : IInventoryManagementService
{
    private const string InventoryReservedTopic = "inventory.reserved";
    private const string StockLevelChangedTopic = "inventory.stock-level-changed";

    private readonly ActivitySource _activitySource = activitySourceProvider.ActivitySource;

    /// <inheritdoc />
    public async Task<IEnumerable<InventoryItem>> GetAllAsync()
    {
        using var activity = _activitySource.StartActivity("GetAllInventory");
        return await dbContext.InventoryItems.ToListAsync();
    }

    /// <inheritdoc />
    public async Task<InventoryItem?> GetByProductIdAsync(string productId)
    {
        using var activity = _activitySource.StartActivity("GetInventoryByProductId");
        activity?.SetTag("product.id", productId);
        return await dbContext.InventoryItems.FindAsync(productId);
    }

    /// <inheritdoc />
    public async Task<InventoryReservedEvent> ReserveInventoryAsync(OrderCreatedEvent orderEvent)
    {
        using var activity = _activitySource.StartActivity("ReserveInventory");
        activity?.SetTag("order.id", orderEvent.OrderId.ToString());
        activity?.SetTag("product.id", orderEvent.ProductId);
        activity?.SetTag("quantity.requested", orderEvent.Quantity);

        var reservedEvent = new InventoryReservedEvent
        {
            OrderId = orderEvent.OrderId,
            ProductId = orderEvent.ProductId,
            Timestamp = DateTime.UtcNow
        };

        var item = await dbContext.InventoryItems.FindAsync(orderEvent.ProductId);
        if (item is not null && item.QuantityAvailable >= orderEvent.Quantity)
        {
            item.QuantityAvailable -= orderEvent.Quantity;
            item.QuantityReserved += orderEvent.Quantity;
            await dbContext.SaveChangesAsync();

            reservedEvent.QuantityReserved = orderEvent.Quantity;
            reservedEvent.Success = true;

            metrics.ReservationSucceeded();
            metrics.InventoryChanged(orderEvent.ProductId);
            activity?.SetTag("reservation.success", true);

            logger.LogInformation("Inventory reserved for order {OrderId}: {Quantity} of {ProductId}",
                orderEvent.OrderId, orderEvent.Quantity, orderEvent.ProductId);
        }
        else
        {
            reservedEvent.Success = false;

            metrics.ReservationFailed();
            activity?.SetTag("reservation.success", false);

            logger.LogWarning("Insufficient inventory for order {OrderId}: requested {Quantity} of {ProductId}",
                orderEvent.OrderId, orderEvent.Quantity, orderEvent.ProductId);
        }

        // Publish event to Kafka
        await eventPublisher.PublishAsync(InventoryReservedTopic, orderEvent.OrderId.ToString(), reservedEvent);
        metrics.EventPublished(InventoryReservedTopic);

        return reservedEvent;
    }

    /// <inheritdoc />
    public async Task<InventoryItem?> RestockAsync(string productId, int additionalQuantity)
    {
        using var activity = _activitySource.StartActivity("RestockInventory");
        activity?.SetTag("product.id", productId);
        activity?.SetTag("quantity.additional", additionalQuantity);

        var item = await dbContext.InventoryItems.FindAsync(productId);
        if (item is null) return null;

        var previousQuantity = item.QuantityAvailable;
        item.QuantityAvailable += additionalQuantity;
        await dbContext.SaveChangesAsync();

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

        await eventPublisher.PublishAsync(StockLevelChangedTopic, productId, stockChangedEvent);

        metrics.Restocked();
        metrics.InventoryChanged(productId);
        metrics.EventPublished(StockLevelChangedTopic);

        logger.LogInformation("Restocked {ProductId}: {Previous} -> {New}",
            productId, previousQuantity, item.QuantityAvailable);

        return item;
    }
}
