using Bielu.AspNetCore.AsyncApi.Attributes.Attributes;

namespace Bielu.AspNetCore.AsyncApi.Tests.Fixtures;

/// <summary>
/// Sample message types for testing.
/// </summary>
public class OrderCreatedEvent
{
    /// <summary>
    /// Unique identifier for the order.
    /// </summary>
    public Guid OrderId { get; set; }

    /// <summary>
    /// Customer who placed the order.
    /// </summary>
    public string CustomerName { get; set; } = string.Empty;

    /// <summary>
    /// Total amount of the order.
    /// </summary>
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// Timestamp when the order was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Order line items.
    /// </summary>
    public List<OrderItem> Items { get; set; } = new();
}

public class OrderItem
{
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public class OrderCancelledEvent
{
    public Guid OrderId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime CancelledAt { get; set; }
}

public class PaymentProcessedEvent
{
    public Guid PaymentId { get; set; }
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public PaymentStatus Status { get; set; }
}

public enum PaymentStatus
{
    Pending,
    Completed,
    Failed,
    Refunded
}

/// <summary>
/// Sample AsyncAPI class with multiple channels and operations.
/// </summary>
[AsyncApi]
public class OrderEventHandler
{
    [Channel("orders/created", Description = "Channel for order creation events")]
    [SubscribeOperation(typeof(OrderCreatedEvent), Summary = "Subscribe to order creation events")]
    public void HandleOrderCreated(OrderCreatedEvent orderEvent)
    {
    }

    [Channel("orders/cancelled", Description = "Channel for order cancellation events")]
    [SubscribeOperation(typeof(OrderCancelledEvent), Summary = "Subscribe to order cancellation events")]
    public void HandleOrderCancelled(OrderCancelledEvent orderEvent)
    {
    }
}

[AsyncApi]
public class OrderEventPublisher
{
    [Channel("orders/created", Description = "Channel for order creation events")]
    [PublishOperation(typeof(OrderCreatedEvent), Summary = "Publish order creation events")]
    public void PublishOrderCreated(OrderCreatedEvent orderEvent)
    {
    }
}

[AsyncApi]
[Channel("payments/processed", Description = "Channel for payment events")]
public class PaymentEventHandler
{
    [Message(typeof(PaymentProcessedEvent))]
    [SubscribeOperation(OperationId = "handlePaymentProcessed", Summary = "Handle payment processed events")]
    public void HandlePaymentProcessed(PaymentProcessedEvent paymentEvent)
    {
    }
}

/// <summary>
/// Sample class with server-specific channels.
/// </summary>
[AsyncApi]
public class MultiServerHandler
{
    [Channel("notifications/email", Description = "Email notification channel", Servers = new[] { "smtp-server" })]
    [PublishOperation(Summary = "Send email notifications")]
    [Message(typeof(EmailNotification))]
    public void SendEmailNotification(EmailNotification notification)
    {
    }

    [Channel("notifications/push", Description = "Push notification channel", Servers = new[] { "websocket-server" })]
    [PublishOperation(Summary = "Send push notifications")]
    [Message(typeof(PushNotification))]
    public void SendPushNotification(PushNotification notification)
    {
    }
}

public class EmailNotification
{
    public string To { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}

public class PushNotification
{
    public string DeviceToken { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Sample class with channel parameters.
/// </summary>
[AsyncApi]
[Channel("users/{userId}/events", Description = "User-specific events channel")]
[ChannelParameter("userId", typeof(string), Description = "The user identifier")]
public class UserEventHandler
{
    [Message(typeof(UserUpdatedEvent))]
    [SubscribeOperation(OperationId = "handleUserEvent", Summary = "Handle user-specific events")]
    public void HandleUserEvent(UserUpdatedEvent userEvent)
    {
    }
}

public class UserUpdatedEvent
{
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Interface-based AsyncAPI definition.
/// </summary>
[AsyncApi]
[Channel("inventory/updates", Description = "Inventory update events")]
public interface IInventoryEventHandler
{
    [Message(typeof(InventoryUpdatedEvent))]
    [SubscribeOperation(Summary = "Handle inventory updates")]
    void HandleInventoryUpdate(InventoryUpdatedEvent inventoryEvent);
}

public class InventoryUpdatedEvent
{
    public string ProductId { get; set; } = string.Empty;
    public int PreviousQuantity { get; set; }
    public int NewQuantity { get; set; }
    public string WarehouseId { get; set; } = string.Empty;
}

/// <summary>
/// Sample class with multiple messages on a single method.
/// </summary>
[AsyncApi]
[Channel("audit/events", Description = "Audit event channel")]
public class AuditEventHandler
{
    [Message(typeof(AuditLoginEvent))]
    [Message(typeof(AuditLogoutEvent))]
    [Message(typeof(AuditActionEvent))]
    [SubscribeOperation(OperationId = "handleAuditEvents", Summary = "Handle all audit events")]
    public void HandleAuditEvent(object auditEvent)
    {
    }
}

public class AuditLoginEvent
{
    public string UserId { get; set; } = string.Empty;
    public DateTime LoginTime { get; set; }
    public string IpAddress { get; set; } = string.Empty;
}

public class AuditLogoutEvent
{
    public string UserId { get; set; } = string.Empty;
    public DateTime LogoutTime { get; set; }
}

public class AuditActionEvent
{
    public string UserId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}
