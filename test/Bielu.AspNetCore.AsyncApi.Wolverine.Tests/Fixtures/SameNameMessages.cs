namespace Bielu.AspNetCore.AsyncApi.Wolverine.Tests.Fixtures.Orders
{
    /// <summary>Published when an order is created.</summary>
    /// <param name="OrderId">The created order.</param>
    public sealed record Created(Guid OrderId);
}

namespace Bielu.AspNetCore.AsyncApi.Wolverine.Tests.Fixtures.Billing
{
    /// <summary>Published when an invoice is created.</summary>
    /// <param name="InvoiceId">The created invoice.</param>
    public sealed record Created(Guid InvoiceId);
}
