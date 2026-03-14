// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Bielu.AspNetCore.AsyncApi.Aspire.NotificationService.Features.Notifications.Events;

/// <summary>
/// Notification sent to clients when inventory levels change.
/// </summary>
public class InventoryAlertNotification
{
    /// <summary>
    /// The product identifier.
    /// </summary>
    public string ProductId { get; set; } = string.Empty;

    /// <summary>
    /// The product name.
    /// </summary>
    public string ProductName { get; set; } = string.Empty;

    /// <summary>
    /// Current available quantity.
    /// </summary>
    public int CurrentQuantity { get; set; }

    /// <summary>
    /// Alert severity level (e.g. "Low", "Critical", "OutOfStock").
    /// </summary>
    public string Severity { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable alert message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the alert was generated.
    /// </summary>
    public DateTime Timestamp { get; set; }
}
