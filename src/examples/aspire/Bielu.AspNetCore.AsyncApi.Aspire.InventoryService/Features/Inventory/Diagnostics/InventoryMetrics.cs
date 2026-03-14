// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.Metrics;

namespace Bielu.AspNetCore.AsyncApi.Aspire.InventoryService.Features.Inventory.Diagnostics;

/// <summary>
/// OpenTelemetry-compatible metrics for the Inventory Service.
/// </summary>
public sealed class InventoryMetrics
{
    public const string MeterName = "MiniShop.InventoryService";

    private readonly Counter<long> _reservationsSucceeded;
    private readonly Counter<long> _reservationsFailed;
    private readonly Counter<long> _restocks;
    private readonly Counter<long> _inventoryChanges;

    public InventoryMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _reservationsSucceeded = meter.CreateCounter<long>(
            "inventory.reservations_succeeded",
            unit: "{reservation}",
            description: "Total number of successful inventory reservations");

        _reservationsFailed = meter.CreateCounter<long>(
            "inventory.reservations_failed",
            unit: "{reservation}",
            description: "Total number of failed inventory reservations (insufficient stock)");

        _restocks = meter.CreateCounter<long>(
            "inventory.restocks",
            unit: "{restock}",
            description: "Total number of restock operations");

        _inventoryChanges = meter.CreateCounter<long>(
            "inventory.changes",
            unit: "{change}",
            description: "Total number of inventory quantity changes");
    }

    public void ReservationSucceeded() => _reservationsSucceeded.Add(1);
    public void ReservationFailed() => _reservationsFailed.Add(1);
    public void Restocked() => _restocks.Add(1);
    public void InventoryChanged(string productId) => _inventoryChanges.Add(1, new KeyValuePair<string, object?>("product_id", productId));
}
