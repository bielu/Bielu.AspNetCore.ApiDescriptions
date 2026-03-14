// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Bielu.AspNetCore.AsyncApi.Aspire.ServiceDefaults.Diagnostics;

/// <summary>
/// Central registry of OpenTelemetry ActivitySource names used across the Mini Shop services.
/// Keeps source names DRY — every registration and usage references this single class.
/// </summary>
public static class DiagnosticsNames
{
    public const string Messaging = "MiniShop.Messaging";
    public const string OrderService = "MiniShop.OrderService";
    public const string InventoryService = "MiniShop.InventoryService";
    public const string NotificationService = "MiniShop.NotificationService";
}
