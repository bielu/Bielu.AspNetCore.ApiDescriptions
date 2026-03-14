// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bielu.AspNetCore.AsyncApi.Aspire.OrderService.Features.Orders.Events;
using Bielu.AspNetCore.AsyncApi.Aspire.OrderService.Features.Orders.Models;
using Bielu.AspNetCore.AsyncApi.Aspire.OrderService.Features.Orders.Services;
using Bielu.AspNetCore.AsyncApi.Attributes;
using Bielu.AspNetCore.AsyncApi.Attributes.Attributes;
using Microsoft.AspNetCore.Mvc;

namespace Bielu.AspNetCore.AsyncApi.Aspire.OrderService.Features.Orders;

/// <summary>
/// Controller for managing orders. Delegates all business logic to <see cref="IOrderService"/>.
/// </summary>
[AsyncApi]
[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private const string OrderCreatedTopic = "orders.created";
    private const string OrderStatusChangedTopic = "orders.status-changed";

    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    /// <summary>
    /// Get all orders.
    /// </summary>
    [HttpGet]
    public async Task<IEnumerable<Order>> GetAll()
    {
        return await _orderService.GetAllAsync();
    }

    /// <summary>
    /// Get a specific order by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Order>> GetById(Guid id)
    {
        var order = await _orderService.GetByIdAsync(id);
        if (order is null) return NotFound();
        return order;
    }

    /// <summary>
    /// Create a new order. Publishes an OrderCreatedEvent to Kafka.
    /// </summary>
    [Channel(OrderCreatedTopic, Servers = ["kafka"])]
    [PublishOperation(typeof(OrderCreatedEvent), "OrderCreated", BindingsRef = "kafkaOrderChannel")]
    [HttpPost]
    public async Task<ActionResult<Order>> Create([FromBody] Order order)
    {
        var created = await _orderService.CreateAsync(order);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Update the status of an order. Publishes an OrderStatusChangedEvent to Kafka.
    /// </summary>
    [Channel(OrderStatusChangedTopic, Servers = ["kafka"])]
    [PublishOperation(typeof(OrderStatusChangedEvent), "OrderStatusChanged", BindingsRef = "kafkaOrderChannel")]
    [HttpPut("{id:guid}/status")]
    public async Task<ActionResult> UpdateStatus(Guid id, [FromBody] string newStatus)
    {
        var order = await _orderService.UpdateStatusAsync(id, newStatus);
        if (order is null) return NotFound();
        return Ok(new { id, status = order.Status });
    }
}
