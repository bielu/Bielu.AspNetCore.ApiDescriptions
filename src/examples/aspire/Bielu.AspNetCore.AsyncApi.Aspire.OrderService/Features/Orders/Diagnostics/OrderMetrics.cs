// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.Metrics;

namespace Bielu.AspNetCore.AsyncApi.Aspire.OrderService.Features.Orders.Diagnostics;

/// <summary>
/// OpenTelemetry-compatible metrics for the Order Service.
/// </summary>
public sealed class OrderMetrics
{
    public const string MeterName = "MiniShop.OrderService";

    private readonly Counter<long> _ordersCreated;
    private readonly Counter<long> _ordersFailed;
    private readonly Counter<long> _statusUpdates;
    private readonly Counter<long> _statusUpdatesFailed;

    public OrderMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _ordersCreated = meter.CreateCounter<long>(
            "orders.created",
            unit: "{order}",
            description: "Total number of orders successfully created");

        _ordersFailed = meter.CreateCounter<long>(
            "orders.create_failed",
            unit: "{order}",
            description: "Total number of order creation failures");

        _statusUpdates = meter.CreateCounter<long>(
            "orders.status_updated",
            unit: "{order}",
            description: "Total number of order status updates");

        _statusUpdatesFailed = meter.CreateCounter<long>(
            "orders.status_update_failed",
            unit: "{order}",
            description: "Total number of failed order status updates");
    }

    public void OrderCreated() => _ordersCreated.Add(1);
    public void OrderCreateFailed() => _ordersFailed.Add(1);
    public void StatusUpdated() => _statusUpdates.Add(1);
    public void StatusUpdateFailed() => _statusUpdatesFailed.Add(1);
}
