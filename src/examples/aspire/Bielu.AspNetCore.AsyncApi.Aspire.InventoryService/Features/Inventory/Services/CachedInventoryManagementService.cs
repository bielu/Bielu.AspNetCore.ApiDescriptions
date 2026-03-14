// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Bielu.AspNetCore.AsyncApi.Aspire.InventoryService.Features.Inventory.Events;
using Bielu.AspNetCore.AsyncApi.Aspire.InventoryService.Features.Inventory.Models;
using StackExchange.Redis;

namespace Bielu.AspNetCore.AsyncApi.Aspire.InventoryService.Features.Inventory.Services;

/// <summary>
/// Caching decorator for <see cref="IInventoryManagementService"/> using Valkey (Redis-compatible).
/// Registered via Scrutor's <c>Decorate</c> — wraps the real service transparently.
/// </summary>
public class CachedInventoryManagementService(
    IInventoryManagementService inner,
    IConnectionMultiplexer redis,
    ILogger<CachedInventoryManagementService> logger) : IInventoryManagementService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    /// <inheritdoc />
    public async Task<IEnumerable<InventoryItem>> GetAllAsync()
    {
        const string cacheKey = "inventory:all";
        var db = redis.GetDatabase();

        var cached = await db.StringGetAsync(cacheKey);
        if (cached.HasValue)
        {
            try
            {
                var items = JsonSerializer.Deserialize<List<InventoryItem>>(cached.ToString());
                if (items is not null)
                {
                    logger.LogDebug("Cache hit for {CacheKey}", cacheKey);
                    return items;
                }
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Failed to deserialize cached inventory list, falling back to source");
            }
        }

        var result = await inner.GetAllAsync();
        var materialized = result.ToList();

        await db.StringSetAsync(cacheKey, JsonSerializer.Serialize(materialized), CacheDuration);
        return materialized;
    }

    /// <inheritdoc />
    public async Task<InventoryItem?> GetByProductIdAsync(string productId)
    {
        var cacheKey = $"inventory:{productId}";
        var db = redis.GetDatabase();

        var cached = await db.StringGetAsync(cacheKey);
        if (cached.HasValue)
        {
            try
            {
                var item = JsonSerializer.Deserialize<InventoryItem>(cached.ToString());
                if (item is not null)
                {
                    logger.LogDebug("Cache hit for {CacheKey}", cacheKey);
                    return item;
                }
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Failed to deserialize cached inventory item {ProductId}, falling back to source", productId);
            }
        }

        var result = await inner.GetByProductIdAsync(productId);
        if (result is not null)
        {
            await db.StringSetAsync(cacheKey, JsonSerializer.Serialize(result), CacheDuration);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<InventoryReservedEvent> ReserveInventoryAsync(OrderCreatedEvent orderEvent)
    {
        var result = await inner.ReserveInventoryAsync(orderEvent);

        // Invalidate caches after mutation
        await InvalidateProductCacheAsync(orderEvent.ProductId);

        return result;
    }

    /// <inheritdoc />
    public async Task<InventoryItem?> RestockAsync(string productId, int additionalQuantity)
    {
        var result = await inner.RestockAsync(productId, additionalQuantity);

        if (result is not null)
        {
            // Invalidate caches after mutation
            await InvalidateProductCacheAsync(productId);
        }

        return result;
    }

    private async Task InvalidateProductCacheAsync(string productId)
    {
        var db = redis.GetDatabase();
        await db.KeyDeleteAsync($"inventory:{productId}");
        await db.KeyDeleteAsync("inventory:all");
        logger.LogDebug("Invalidated cache for product {ProductId}", productId);
    }
}
