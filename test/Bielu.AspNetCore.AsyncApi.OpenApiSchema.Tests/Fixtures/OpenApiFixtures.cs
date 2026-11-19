using System.ComponentModel.DataAnnotations;
using Bielu.AspNetCore.AsyncApi.Attributes;

namespace Bielu.AspNetCore.AsyncApi.OpenApiSchema.Tests.Fixtures;

public enum OrderStatus
{
    Placed,
    Shipped,
}

/// <summary>An order line.</summary>
/// <param name="Sku">The product.</param>
/// <param name="Quantity">How many.</param>
public sealed record OrderLine(string Sku, int Quantity);

/// <summary>A node of a category tree.</summary>
/// <param name="Name">The category name.</param>
/// <param name="Children">Its sub-categories.</param>
public sealed record Category(string Name, List<Category> Children);

/// <summary>An order was placed.</summary>
public sealed class OrderPlaced
{
    /// <summary>The order.</summary>
    public Guid OrderId { get; set; }

    /// <summary>When it was placed.</summary>
    public DateTimeOffset PlacedAt { get; set; }

    /// <summary>The order total.</summary>
    [Range(0, 1000, MinimumIsExclusive = true)]
    public double Total { get; set; }

    /// <summary>An optional note.</summary>
    public string? Note { get; set; }

    /// <summary>An optional discount.</summary>
    public int? Discount { get; set; }

    /// <summary>The order status.</summary>
    public OrderStatus Status { get; set; }

    /// <summary>The order lines.</summary>
    public List<OrderLine> Lines { get; set; } = [];

    /// <summary>Free-form attributes.</summary>
    public Dictionary<string, string> Attributes { get; set; } = [];

    /// <summary>The order's category.</summary>
    public Category? Category { get; set; }
}

public sealed record OrderAudited(string OrderId);

[AsyncApi(OpenApiFixtureChannels.DocumentName)]
public sealed class OpenApiFixtureChannels
{
    public const string DocumentName = "openapi";

    [Channel("orders/placed")]
    [Message(typeof(OrderPlaced), MessageId = "orderPlaced")]
    [SendOperation(typeof(OrderPlaced), OperationId = "orderPlacedOperation")]
    public void OrderPlacedEvent()
    {
    }

    [Channel("orders/audited")]
    [Message(typeof(OrderAudited), MessageId = "orderAudited")]
    [SendOperation(typeof(OrderAudited), OperationId = "orderAuditedOperation")]
    public void OrderAuditedEvent()
    {
    }
}
