using Wolverine.Attributes;

namespace Bielu.AspNetCore.AsyncApi.Wolverine.Tests.Fixtures;

// The handlers here are named outside Wolverine's handler conventions, so discovery leaves them out and
// only the tests that register them (HandlerDiscovery.IncludeType) and bind them to an endpoint see them.

/// <summary>Read from two Kafka topics, each with a handler of its own that answers differently.</summary>
/// <param name="Sku">The product to price.</param>
[MessageIdentity("pricing.price-request")]
public sealed record PriceRequest(string Sku);

/// <summary>The retail price of a product.</summary>
/// <param name="Sku">The priced product.</param>
/// <param name="Price">The retail price.</param>
[MessageIdentity("pricing.retail-price")]
public sealed record RetailPrice(string Sku, decimal Price);

/// <summary>The wholesale price of a product.</summary>
/// <param name="Sku">The priced product.</param>
/// <param name="Price">The wholesale price.</param>
[MessageIdentity("pricing.wholesale-price")]
public sealed record WholesalePrice(string Sku, decimal Price);

public static class RetailPriceDesk
{
    public static RetailPrice Handle(PriceRequest request) => new(request.Sku, 0m);
}

public static class WholesalePriceDesk
{
    public static WholesalePrice Handle(PriceRequest request) => new(request.Sku, 0m);
}

/// <summary>Read from Kafka and relayed to handlers on local queues.</summary>
/// <param name="Sku">The product to audit.</param>
[MessageIdentity("audit.audit-request")]
public sealed record AuditRequest(string Sku);

/// <summary>The outcome of an audit.</summary>
/// <param name="Sku">The audited product.</param>
[MessageIdentity("audit.audit-result")]
public sealed record AuditResult(string Sku);

public static class StockAuditor
{
    public static AuditResult Handle(AuditRequest request) => new(request.Sku);
}

public static class PriceAuditor
{
    public static AuditResult Handle(AuditRequest request) => new(request.Sku);
}
