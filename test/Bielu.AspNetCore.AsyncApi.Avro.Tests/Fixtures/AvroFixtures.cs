using Bielu.AspNetCore.AsyncApi.Attributes;

namespace Bielu.AspNetCore.AsyncApi.Avro.Tests.Fixtures;

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
/// <param name="OrderId">The order.</param>
/// <param name="PlacedAt">When it was placed.</param>
/// <param name="DeliveryDate">The requested delivery date.</param>
/// <param name="Total">The order total.</param>
/// <param name="Weight">The parcel weight.</param>
/// <param name="Express">Whether it ships express.</param>
/// <param name="Note">An optional note.</param>
/// <param name="Discount">An optional discount.</param>
/// <param name="Status">The order status.</param>
/// <param name="Lines">The order lines.</param>
/// <param name="Attributes">Free-form attributes.</param>
/// <param name="Signature">The raw signature.</param>
/// <param name="Category">The order's category.</param>
public sealed record OrderPlaced(
    Guid OrderId,
    DateTimeOffset PlacedAt,
    DateOnly DeliveryDate,
    long Total,
    double Weight,
    bool Express,
    string? Note,
    int? Discount,
    OrderStatus Status,
    List<OrderLine> Lines,
    Dictionary<string, string> Attributes,
    byte[] Signature,
    Category Category);

public sealed record OrderAudited(string OrderId);

[AsyncApi(AvroFixtureChannels.DocumentName)]
public sealed class AvroFixtureChannels
{
    public const string DocumentName = "avro";

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
