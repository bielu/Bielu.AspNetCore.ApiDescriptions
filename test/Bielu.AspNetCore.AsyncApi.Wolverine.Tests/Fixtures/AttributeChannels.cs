using Bielu.AspNetCore.AsyncApi.Attributes.Attributes;

namespace Bielu.AspNetCore.AsyncApi.Wolverine.Tests.Fixtures;

/// <summary>Attribute-declared channels that share a document with Wolverine's routing.</summary>
public static class AttributeChannels
{
    /// <summary>A document whose attribute payload has the same simple name as a Wolverine message.</summary>
    public const string ClashingDocument = "mixed-clashing";

    /// <summary>A document whose attribute payload is also routed by Wolverine.</summary>
    public const string SharedDocument = "mixed-shared";

    [AsyncApi(ClashingDocument)]
    public sealed class Invoices
    {
        [Channel("invoices/created")]
        [Message(typeof(Billing.Created), MessageId = "invoiceCreated")]
        [SubscribeOperation(OperationId = "invoiceCreatedOperation")]
        public void InvoiceCreated()
        {
        }
    }

    [AsyncApi(SharedDocument)]
    public sealed class Orders
    {
        [Channel("orders/created")]
        [Message(typeof(Fixtures.Orders.Created), MessageId = "orderCreated")]
        [SubscribeOperation(OperationId = "orderCreatedOperation")]
        public void OrderCreated()
        {
        }
    }
}
