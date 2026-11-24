using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Bielu.Arazzo.Models;
using Bielu.AspNetCore.Arazzo.Services;
using Bielu.AspNetCore.AsyncApi.Attributes;
using Bielu.AspNetCore.AsyncApi.Extensions;
using Bielu.AspNetCore.AsyncApi.Extensions.Protocols.SignalR;
using ByteBard.AsyncAPI.Models;
using ByteBard.AsyncAPI.Models.Interfaces;
using Microsoft.AspNetCore.SignalR;

/// <summary>
/// An order accepted now and settled later: the storefront gets <c>202 Accepted</c> straight away, the inventory
/// service decides in the background whether there is stock, and the orders service then tells the browser over
/// SignalR and emails the customer.
/// </summary>
/// <remarks>
/// The SignalR hub (<see cref="OrderUpdatesHub" />) has an AsyncAPI document of its own, <c>updates</c>, with the
/// SignalR protocol bindings; the email service has an OpenAPI document of its own, <c>email</c>. Submitting an
/// order really does push its update over the hub, a moment later, so a browser connected to
/// <c>/hubs/orders</c> sees it arrive.
/// </remarks>
internal static class AsyncOrder
{
    /// <summary>Where the browser connects for order updates.</summary>
    public const string HubPath = "/hubs/orders";

    /// <summary>Adds SignalR, the hub's AsyncAPI document and the email service's OpenAPI document.</summary>
    public static IServiceCollection AddAsyncOrderServices(this IServiceCollection services)
    {
        services.AddSignalR();
        services.AddOpenApi("email", options => options.AddDocumentTransformer((document, _, _) =>
        {
            document.Info.Title = "Email service";
            return Task.CompletedTask;
        }));
        services.AddAsyncApi("updates", options =>
        {
            options.WithInfo("Order updates", "1.0.0")
                .WithDescription("Pushed to the browser over SignalR when an order's status changes.");
            options.AddServer("signalr", "localhost:5000", SignalRProtocol.ProtocolName, pathName: null, server =>
            {
                server.Description = "The storefront's SignalR endpoint";
                server.Bindings = new AsyncApiBindings<IServerBinding>
                {
                    new SignalRServerBinding { Transports = { SignalRProtocol.Transports.WebSockets }, Protocols = { SignalRProtocol.HubProtocols.Json } },
                };
            });
            // Only the hub: the app's other [AsyncApi] channels belong to the events document.
            options.AddIncludedChannel("orderUpdates");
            options.AddSignalRChannelBinding("orderUpdates", channel => channel.Hub = HubPath);
            options.AddSignalROperationBinding("orderStatusChanged", operation =>
            {
                operation.Target = nameof(IOrderUpdatesClient.OrderStatusChanged);
                operation.Direction = SignalRProtocol.Directions.ServerToClient;
            });
        });
        return services;
    }

    /// <summary>Adds the hub and the email service as sources, and the asynchronous order workflow.</summary>
    public static ArazzoOptions AddAsyncOrderWorkflow(this ArazzoOptions options)
    {
        options.AddAsyncApiSource("updates", "updates");
        options.AddOpenApiSource("email", "email");

        // Every step waits on the one it needs (dependsOn), so the notification and the email, which both only
        // need the stock result, run at the same time.
        options.AddWorkflow("placeOrderAsync", workflow => workflow
            .WithSummary("Place an order, settled later")
            .WithDescription(
                "The storefront submits the order and gets 202 Accepted at once. The inventory service checks the stock in the background; once it answers, the orders service pushes the new status to the browser over SignalR and emails the customer.")
            .Step("submit", step => step
                .Operation("submitOrder")
                .WithDescription("Submit the order; it is accepted, not yet confirmed.")
                .Payload(new JsonObject { ["sku"] = "SKU-1", ["quantity"] = 1, ["email"] = "customer@example.com" }, "application/json")
                .SuccessCriteria("$statusCode == 202")
                .OnFailure(new ArazzoFailureAction
                {
                    Name = "rejected",
                    Type = "end",
                    Criteria = [new ArazzoCriterion { Condition = "$statusCode == 400" }],
                })
                .Output("orderId", "$response.body#/id"))
            .Step("placed", step => step
                .Channel("events", "orderPlaced", ArazzoStepAction.Send)
                .WithDescription("The orders service announces the pending order.")
                .WithExtension(Checkout.Actor, "orders")
                .DependsOn("submit"))
            .Step("checkStock", step => step
                .Channel("events", "orderPlaced", ArazzoStepAction.Receive)
                .WithDescription("The inventory service picks the order up and checks its stock.")
                .WithExtension(Checkout.Actor, "inventory")
                .WithCorrelationId("$steps.submit.outputs.orderId")
                .DependsOn("placed"))
            .Step("stockChecked", step => step
                .Channel("events", "stockChecked", ArazzoStepAction.Send)
                .WithDescription("The inventory service reports whether the stock is there.")
                .WithExtension(Checkout.Actor, "inventory")
                .DependsOn("checkStock"))
            .Step("stockResult", step => step
                .Channel("events", "stockChecked", ArazzoStepAction.Receive)
                .WithDescription("The orders service learns the result and settles the order.")
                .WithExtension(Checkout.Actor, "orders")
                .WithCorrelationId("$steps.submit.outputs.orderId")
                .WithTimeout(30_000)
                // A comparison through a JSON Pointer: the payload is one object, which a JSONPath filter
                // ($[?@.status == ...]) would look inside of rather than test, so it would never match.
                .SuccessCriteria("$message.payload#/status == 'Reserved'")
                .OnFailure(new ArazzoFailureAction
                {
                    Name = "outOfStock",
                    Type = "end",
                    Criteria = [new ArazzoCriterion { Condition = "$message.payload#/status == 'OutOfStock'" }],
                })
                .DependsOn("stockChecked"))
            .Step("notify", step => step
                .Channel("updates", "orderUpdates", ArazzoStepAction.Send)
                .WithDescription("Push the confirmed status to the browser over SignalR.")
                .WithExtension(Checkout.Actor, "orders")
                .DependsOn("stockResult"))
            .Step("email", step => step
                .Operation("sendEmail")
                .WithDescription("Email the customer that the order is confirmed.")
                .WithExtension(Checkout.Actor, "orders")
                .Payload(new JsonObject { ["template"] = "order-confirmed", ["orderId"] = null }, "application/json")
                .Replacement("/orderId", "$steps.submit.outputs.orderId")
                .SuccessCriteria("$statusCode == 202")
                .DependsOn("stockResult"))
            .Step("updated", step => step
                .Channel("updates", "orderUpdates", ArazzoStepAction.Receive)
                .WithDescription("The browser receives the new status on the open page.")
                .WithCorrelationId("$steps.submit.outputs.orderId")
                .WithTimeout(60_000)
                .DependsOn("notify")));
        return options;
    }

    /// <summary>The order submission endpoint, the email service, and the hub.</summary>
    public static IEndpointRouteBuilder MapAsyncOrder(this IEndpointRouteBuilder app)
    {
        app.MapPost("/async-orders", (OrderSubmission submission, IHubContext<OrderUpdatesHub, IOrderUpdatesClient> hub) =>
            {
                var id = Guid.NewGuid();
                // Stands in for the inventory check: a moment later, the browser hears that the order went through.
                _ = Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(2));
                    await hub.Clients.All.OrderStatusChanged(new OrderStatusChanged(id, OrderStatus.Confirmed, DateTimeOffset.UtcNow));
                });
                return Results.Accepted($"/orders/{id}", new SubmittedOrder(id, submission.Sku, submission.Quantity, OrderStatus.Pending));
            })
            // In the orders service's document only (Program.cs says why).
            .WithGroupName("v1")
            .WithName("submitOrder")
            .Produces<SubmittedOrder>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        app.MapPost("/emails", (EmailRequest request) => Results.Accepted())
            .WithGroupName("email")
            .WithName("sendEmail")
            .Produces(StatusCodes.Status202Accepted);

        app.MapHub<OrderUpdatesHub>(HubPath);
        return app;
    }
}

/// <summary>Where an order stands.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<OrderStatus>))]
public enum OrderStatus
{
    Pending,
    Confirmed,
    Rejected,
}

[JsonConverter(typeof(JsonStringEnumConverter<StockStatus>))]
internal enum StockStatus
{
    Reserved,
    OutOfStock,
}

internal sealed record OrderSubmission(string Sku, int Quantity, string Email);

internal sealed record SubmittedOrder(Guid Id, string Sku, int Quantity, OrderStatus Status);

internal sealed record EmailRequest(string Template, Guid OrderId);

internal sealed record StockChecked(Guid OrderId, StockStatus Status, DateTimeOffset CheckedAt);

/// <summary>An order's new status, pushed to the browser.</summary>
public sealed record OrderStatusChanged(Guid OrderId, OrderStatus Status, DateTimeOffset ChangedAt);

/// <summary>What the hub pushes to the browser.</summary>
[AsyncApi("updates")]
[Channel("orderUpdates", BindingsRef = "orderUpdates", Description = "Order status updates, pushed to the storefront's open pages.")]
public interface IOrderUpdatesClient
{
    /// <summary>An order's status changed.</summary>
    [SendOperation(typeof(OrderStatusChanged), OperationId = "orderStatusChanged", Summary = "An order's status changed.", BindingsRef = "orderStatusChanged")]
    Task OrderStatusChanged(OrderStatusChanged update);
}

/// <summary>The storefront's order updates; it only pushes, the browser sends nothing.</summary>
public sealed class OrderUpdatesHub : Hub<IOrderUpdatesClient>
{
}

[AsyncApi]
internal sealed class StockEvents
{
    [Channel("stockChecked", Description = "Stock checks of submitted orders, by the inventory service.")]
    [Message(typeof(StockChecked), Title = "Stock checked", Summary = "Whether an order's items are in stock.", ContentType = "application/json")]
    [SendOperation(typeof(StockChecked), OperationId = "onStockChecked")]
    public void StockCheckedEvent()
    {
    }
}
