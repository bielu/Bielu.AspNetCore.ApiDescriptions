// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bielu.AspNetCore.AsyncApi.Aspire.InventoryService.Features.Inventory.Events;
using Bielu.AspNetCore.AsyncApi.Aspire.InventoryService.Features.Inventory.Models;
using Bielu.AspNetCore.AsyncApi.Aspire.ServiceDefaults.Caching;

namespace Bielu.AspNetCore.AsyncApi.Aspire.InventoryService.Features.Inventory.Services;

/// <summary>
/// Caching decorator for <see cref="IInventoryManagementService"/> using Valkey (Redis-compatible).
/// Registered via Scrutor's <c>Decorate</c> — wraps the real service transparently.
/// Uses the shared <see cref="ICacheService"/> from ServiceDefaults.
/// </summary>
public class CachedInventoryManagementService(
    IInventoryManagementService inner,
    ICacheService cache,
    ILogger<CachedInventoryManagementService> logger) : IInventoryManagementService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    /// <inheritdoc />
    public async Task<IEnumerable<InventoryItem>> GetAllAsync()
    {
        const string cacheKey = "inventory:all";

        var cached = await cache.GetAsync<List<InventoryItem>>(cacheKey);
        if (cached is not null)
            return cached;

        var result = await inner.GetAllAsync();
        var materialized = result.ToList();

        await cache.SetAsync(cacheKey, materialized, CacheDuration);
        return materialized;
    }

    /// <inheritdoc />
    public async Task<InventoryItem?> GetByProductIdAsync(string productId)
    {
        var cacheKey = $"inventory:{productId}";

        var cached = await cache.GetAsync<InventoryItem>(cacheKey);
        if (cached is not null)
            return cached;

        var result = await inner.GetByProductIdAsync(productId);
        if (result is not null)
        {
            await cache.SetAsync(cacheKey, result, CacheDuration);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<InventoryReservedEvent> ReserveInventoryAsync(OrderCreatedEvent orderEvent)
    {
        var result = await inner.ReserveInventoryAsync(orderEvent);

        // Invalidate caches after mutation
        await cache.RemoveAsync([$"inventory:{orderEvent.ProductId}", "inventory:all"]);

        return result;
    }

    /// <inheritdoc />
    public async Task<InventoryItem?> RestockAsync(string productId, int additionalQuantity)
    {
        var result = await inner.RestockAsync(productId, additionalQuantity);

        if (result is not null)
        {
            // Invalidate caches after mutation
            await cache.RemoveAsync([$"inventory:{productId}", "inventory:all"]);
        }

        return result;
    }
}
