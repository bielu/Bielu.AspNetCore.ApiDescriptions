// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Bielu.AspNetCore.AsyncApi.Aspire.ServiceDefaults.Caching;

/// <summary>
/// Abstraction for distributed caching operations.
/// Follows the same DRY principle as <c>IEventPublisher</c> —
/// services depend on this rather than directly on <c>IConnectionMultiplexer</c>.
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Get a cached value by key.
    /// Returns <c>null</c> if the key does not exist or deserialization fails.
    /// </summary>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Set a value in the cache with the specified expiration.
    /// </summary>
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Remove a single key from the cache.
    /// </summary>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove multiple keys from the cache concurrently.
    /// </summary>
    Task RemoveAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default);
}
