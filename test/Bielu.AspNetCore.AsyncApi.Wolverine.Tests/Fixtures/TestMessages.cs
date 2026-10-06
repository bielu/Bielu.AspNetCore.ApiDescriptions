using Wolverine;
using Wolverine.Attributes;
using Wolverine.SignalR;
using Wolverine.SignalR.Internals;

namespace Bielu.AspNetCore.AsyncApi.Wolverine.Tests.Fixtures;

/// <summary>The test app's Wolverine hub.</summary>
public sealed class TestHub(SignalRTransport transport) : WolverineHub(transport);

/// <summary>Pushed to clients when an order ships.</summary>
/// <param name="OrderId">The shipped order.</param>
[MessageIdentity("orders.order-shipped")]
public sealed record OrderShipped(Guid OrderId) : WebSocketMessage;

/// <summary>Pushed to clients without an explicit identity.</summary>
/// <param name="Text">Free text.</param>
public sealed record Notice(string Text) : WebSocketMessage;

/// <summary>Published to Kafka when an order is placed.</summary>
/// <param name="OrderId">The placed order.</param>
[MessageIdentity("orders.order-placed")]
public sealed record OrderPlaced(Guid OrderId);

/// <summary>Read from Kafka, sent by a producer that isn't Wolverine.</summary>
/// <param name="Sku">The product whose stock changed.</param>
/// <param name="Quantity">The new stock level.</param>
[MessageIdentity("stock.stock-changed")]
public sealed record StockChanged(string Sku, int Quantity);

/// <summary>Handled in-process only; never documented.</summary>
/// <param name="OrderId">The order to ship.</param>
public sealed record ShipOrder(Guid OrderId);

public static class ShipOrderHandler
{
    public static OrderShipped Handle(ShipOrder command) => new(command.OrderId);
}

public static class StockChangedHandler
{
    public static void Handle(StockChanged message)
    {
    }
}

/// <summary>Covers the schema shapes generated frontends rely on.</summary>
[MessageIdentity("shapes.shape-probe")]
public sealed record ShapeProbe(int Count, int? MaybeCount, string? Note, DateTimeOffset At, DayOfWeek Day, DayOfWeek? MaybeDay) : WebSocketMessage;
