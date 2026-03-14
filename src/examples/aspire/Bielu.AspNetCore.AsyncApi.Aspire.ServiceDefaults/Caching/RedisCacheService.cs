// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Bielu.AspNetCore.AsyncApi.Aspire.ServiceDefaults.Caching;

/// <summary>
/// Redis/Valkey-backed implementation of <see cref="ICacheService"/>.
/// Handles serialization, error handling, and structured logging for all cache operations.
/// </summary>
public class RedisCacheService(
    IConnectionMultiplexer redis,
    ILogger<RedisCacheService> logger) : ICacheService
{
    /// <inheritdoc />
    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        var db = redis.GetDatabase();
        var cached = await db.StringGetAsync(key);

        if (!cached.HasValue)
            return null;

        try
        {
            var result = JsonSerializer.Deserialize<T>(cached.ToString());
            if (result is not null)
            {
                logger.LogDebug("Cache hit for {CacheKey}", key);
                return result;
            }
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to deserialize cached value for {CacheKey}, returning null", key);
        }

        return null;
    }

    /// <inheritdoc />
    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default) where T : class
    {
        var db = redis.GetDatabase();
        var payload = JsonSerializer.Serialize(value);
        await db.StringSetAsync(key, payload, expiration);
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        var db = redis.GetDatabase();
        await db.KeyDeleteAsync(key);
    }

    /// <inheritdoc />
    public async Task RemoveAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        var db = redis.GetDatabase();
        await Task.WhenAll(keys.Select(k => db.KeyDeleteAsync(k).AsTask()));
        logger.LogDebug("Invalidated cache keys: {CacheKeys}", string.Join(", ", keys));
    }
}
