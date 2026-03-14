// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bielu.AspNetCore.AsyncApi.Aspire.OrderService.Features.Orders.Models;

namespace Bielu.AspNetCore.AsyncApi.Aspire.OrderService.Features.Orders.Services;

/// <summary>
/// Service interface for order management operations.
/// </summary>
public interface IOrderService
{
    /// <summary>
    /// Get all orders ordered by creation date descending.
    /// </summary>
    Task<IEnumerable<Order>> GetAllAsync();

    /// <summary>
    /// Get a specific order by ID, using cache with database fallback.
    /// </summary>
    Task<Order?> GetByIdAsync(Guid id);

    /// <summary>
    /// Create a new order, persist it, cache it, and publish an event.
    /// </summary>
    Task<Order> CreateAsync(Order order);

    /// <summary>
    /// Update an order's status, invalidate cache, and publish an event.
    /// Returns null if the order was not found.
    /// </summary>
    Task<Order?> UpdateStatusAsync(Guid id, string newStatus);
}
