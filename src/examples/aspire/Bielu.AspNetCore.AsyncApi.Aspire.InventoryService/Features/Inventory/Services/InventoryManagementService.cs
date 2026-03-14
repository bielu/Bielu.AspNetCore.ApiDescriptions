// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Bielu.AspNetCore.AsyncApi.Aspire.InventoryService.Features.Inventory.Data;
using Bielu.AspNetCore.AsyncApi.Aspire.InventoryService.Features.Inventory.Events;
using Bielu.AspNetCore.AsyncApi.Aspire.InventoryService.Features.Inventory.Models;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;

namespace Bielu.AspNetCore.AsyncApi.Aspire.InventoryService.Features.Inventory.Services;

/// <summary>
/// Service handling inventory business logic, persistence, and event publishing.
/// </summary>
public class InventoryManagementService : IInventoryManagementService
{
    private const string InventoryReservedTopic = "inventory.reserved";
    private const string StockLevelChangedTopic = "inventory.stock-level-changed";

    private readonly IProducer<string, string> _kafkaProducer;
    private readonly InventoryDbContext _dbContext;
    private readonly ILogger<InventoryManagementService> _logger;

    public InventoryManagementService(
        IProducer<string, string> kafkaProducer,
        InventoryDbContext dbContext,
        ILogger<InventoryManagementService> logger)
    {
        _kafkaProducer = kafkaProducer;
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<InventoryItem>> GetAllAsync()
    {
        return await _dbContext.InventoryItems.ToListAsync();
    }

    /// <inheritdoc />
    public async Task<InventoryItem?> GetByProductIdAsync(string productId)
    {
        return await _dbContext.InventoryItems.FindAsync(productId);
    }

    /// <inheritdoc />
    public async Task<InventoryReservedEvent> ReserveInventoryAsync(OrderCreatedEvent orderEvent)
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
        return reservedEvent;
    }

    /// <inheritdoc />
    public async Task<InventoryItem?> RestockAsync(string productId, int additionalQuantity)
    {
        var item = await _dbContext.InventoryItems.FindAsync(productId);
        if (item is null) return null;

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

        return item;
    }
}
