using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Bielu.Arazzo.Models;
using Bielu.AspNetCore.Arazzo.Extensions;
using Bielu.AspNetCore.Arazzo.Scalar;
using Bielu.AspNetCore.AsyncApi.Attributes;
using Bielu.AspNetCore.AsyncApi.Extensions;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
// The payment gateway and the inventory service of the checkout workflow (Checkout.cs).
builder.Services.AddCheckoutServices();
builder.Services.AddAsyncApi();

// The workflows mix HTTP operations (OpenAPI) and messages (AsyncAPI) of this app. The Scalar plugin
// renders each step next to the operation or channel it calls.
builder.Services.AddArazzo(options =>
{
    options.WithInfo(info =>
    {
        info.Title = "Order workflows";
        info.Version = "1.0.0";
        info.Description = "How a client places an order and follows it to shipment.";
    });
    options.AddOpenApiSource("orders", "v1");
    options.AddAsyncApiSource("events", "v1");
    // A checkout across the orders service, the payment gateway and the inventory service, in three stages.
    options.AddCheckoutWorkflows();

    // The shop's pages, for the "Pages" view of the workflow overview: steps say which page the customer is on
    // (x-page), actions which page they land on, and workflows, steps and actions carry plain-language labels
    // (x-label). See ShopPages and the WithArazzoOptions call below.
    options.WithExtension(ShopPages.Pages, new JsonObject
    {
        ["cart"] = new JsonObject { ["title"] = "Cart", ["route"] = "/cart" },
        ["checkout"] = new JsonObject { ["title"] = "Checkout", ["route"] = "/checkout" },
        ["payment"] = new JsonObject { ["title"] = "Payment", ["route"] = "/checkout/payment" },
        ["paymentDeclined"] = new JsonObject { ["title"] = "Payment declined", ["route"] = "/checkout/declined" },
        ["confirmation"] = new JsonObject { ["title"] = "Order confirmed", ["route"] = "/orders/{id}/confirmation" },
        ["order"] = new JsonObject { ["title"] = "Order status", ["route"] = "/orders/{id}" },
        ["unavailable"] = new JsonObject { ["title"] = "Order unavailable", ["route"] = "/orders/unavailable" },
    });

    // A reusable failure action, referred to below with OnFailureReference.
    options.AddComponentFailureAction("unavailable", new ArazzoFailureAction
    {
        Name = "unavailable",
        Type = "end",
        Extensions = new Dictionary<string, JsonNode?> { [ShopPages.Page] = "unavailable", [ShopPages.Label] = "Sold out" },
    });

    options.AddWorkflow("placeOrder", workflow => workflow
        .WithSummary("Place an order and announce it")
        .WithExtension(ShopPages.Label, "Place an order")
        .WithDescription(
            "Creates the order over HTTP, publishes `orderPlaced`, asks for payment, and waits for the payment and the stock reservation to come back as messages.")
        .WithInputs<PlaceOrderInputs>(new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        })
        .Step("create", step => step
            .Operation("createOrder")
            .WithDescription("Create the order.")
            .WithExtension(ShopPages.Page, "checkout")
            .WithExtension(ShopPages.Label, "Place the order")
            // A payload template whose values are filled in from the inputs before the request is sent.
            .Payload(new JsonObject { ["sku"] = null, ["quantity"] = 1 }, "application/json")
            .Replacement("/sku", "$inputs.sku")
            .Replacement("/quantity", "$inputs.quantity")
            .SuccessCriteria("$statusCode == 201")
            .OnFailure(new ArazzoFailureAction
            {
                Name = "rejected",
                Type = "end",
                Criteria = [new ArazzoCriterion { Condition = "$statusCode == 400" }],
                Extensions = new Dictionary<string, JsonNode?> { [ShopPages.Page] = "cart", [ShopPages.Label] = "Invalid order" },
            })
            .Output("orderId", "$response.body#/id")
            // A Selector Object output (Arazzo 1.1): JSONPath over the response body.
            .Output("total", new ArazzoSelector { Context = "$response.body", Selector = "$.total", Type = "jsonpath" }))
        .Step("announce", step => step
            .Channel("events", "orderPlaced", ArazzoStepAction.Send)
            .WithDescription("Tell the rest of the system about the new order.")
            .WithExtension(ShopPages.Page, "checkout")
            .WithExtension(ShopPages.Label, "Announce the order")
            .PayloadExpression("$steps.create.outputs")
            .DependsOn("create"))
        .Step("requestPayment", step => step
            .Channel("events", "paymentRequested", ArazzoStepAction.Send)
            .WithDescription("Ask the payment service to charge the order total.")
            .WithExtension(ShopPages.Page, "payment")
            .WithExtension(ShopPages.Label, "Ask for payment")
            .Payload(new JsonObject { ["orderId"] = null, ["amount"] = 0, ["currency"] = "EUR" }, "application/json")
            .Replacement("/orderId", "$steps.create.outputs.orderId")
            .Replacement("/amount", "$steps.create.outputs.total")
            .DependsOn("announce"))
        .Step("paymentCompleted", step => step
            .Channel("events", "paymentCompleted", ArazzoStepAction.Receive)
            .WithDescription("Wait for the payment result for this order.")
            .WithExtension(ShopPages.Page, "payment")
            .WithExtension(ShopPages.Label, "Wait for the payment")
            .WithCorrelationId("$steps.create.outputs.orderId")
            .WithTimeout(60_000)
            .SuccessCriteria(
                "$[?@.status == 'Succeeded']",
                "$message.payload",
                new ArazzoSelectorType { Type = "jsonpath", Version = "rfc9535" })
            .OnFailure(new ArazzoFailureAction
            {
                Name = "declined",
                Type = "end",
                Criteria = [new ArazzoCriterion { Condition = "$message.payload.status == 'Declined'" }],
                Extensions = new Dictionary<string, JsonNode?> { [ShopPages.Page] = "paymentDeclined", [ShopPages.Label] = "Card declined" },
            })
            .Output("paymentId", "$message.payload.paymentId")
            .DependsOn("requestPayment"))
        .Step("stockReserved", step => step
            .Channel("events", "inventoryReserved", ArazzoStepAction.Receive)
            .WithDescription("Wait until the warehouse has reserved the stock.")
            .WithExtension(ShopPages.Page, "confirmation")
            .WithExtension(ShopPages.Label, "Reserve the stock")
            .WithCorrelationId("$steps.create.outputs.orderId")
            .WithTimeout(120_000)
            .Output("reservationExpiresAt", "$message.payload.expiresAt")
            .DependsOn("paymentCompleted"))
        // Workflow outputs: what trackOrder reads back as $workflows.placeOrder.outputs.*.
        .Output("orderId", "$steps.create.outputs.orderId")
        .Output("paymentId", "$steps.paymentCompleted.outputs.paymentId"));

    options.AddWorkflow("trackOrder", workflow => workflow
        .WithSummary("Follow an order to shipment")
        .WithExtension(ShopPages.Label, "Follow an order")
        .Step("place", step => step
            .Workflow("placeOrder")
            .WithDescription("Reuse the placeOrder workflow.")
            .Output("orderId", "$workflows.placeOrder.outputs.orderId"))
        .Step("status", step => step
            .Operation("getOrder")
            .WithDescription("Check the order status.")
            .WithExtension(ShopPages.Page, "order")
            .WithExtension(ShopPages.Label, "Check the order")
            // A Selector Object parameter: JSON Pointer into the outputs of the reused workflow.
            .Parameter(new ArazzoParameter
            {
                Name = "id",
                In = ArazzoParameterLocation.Path,
                Value = ArazzoValue.FromSelector(new ArazzoSelector
                {
                    Context = "$workflows.placeOrder.outputs",
                    Selector = "/orderId",
                    Type = "jsonpointer",
                }),
            })
            .SuccessCriteria("$statusCode == 200")
            // A JSONPath criterion (RFC 9535): only a live order counts as found.
            .SuccessCriteria(
                "$[?@.status != 'cancelled']",
                "$response.body",
                new ArazzoSelectorType { Type = "jsonpath", Version = "rfc9535" })
            // Read by the "cancelled" action, which places the order again with the same SKU.
            .Output("sku", "$response.body#/sku")
            // Freshly placed orders can take a moment to appear: retry a 404 a few times before giving up.
            .OnFailure(new ArazzoFailureAction
            {
                Name = "notYetVisible",
                Type = "retry",
                Extensions = new Dictionary<string, JsonNode?> { [ShopPages.Label] = "Not visible yet" },
                StepId = "status",
                RetryAfter = 2,
                RetryLimit = 3,
                Criteria = [new ArazzoCriterion { Condition = "$statusCode == 404" }],
            })
            // A cancelled order is placed again: the action passes parameters to the workflow it starts.
            .OnFailure(new ArazzoFailureAction
            {
                Name = "cancelled",
                Type = "goto",
                Extensions = new Dictionary<string, JsonNode?> { [ShopPages.Label] = "Order cancelled" },
                WorkflowId = "placeOrder",
                Criteria = [new ArazzoCriterion { Condition = "$statusCode == 410" }],
                Parameters =
                [
                    new ArazzoReferenceable<ArazzoParameter>
                    {
                        Value = new ArazzoParameter { Name = "sku", Value = "$steps.status.outputs.sku" },
                    },
                ],
            })
            .OnFailureReference("unavailable"))
        .Step("shipped", step => step
            .Channel("events", "orderShipped", ArazzoStepAction.Receive)
            .WithDescription("Wait for the shipment notification.")
            .WithExtension(ShopPages.Page, "order")
            .WithExtension(ShopPages.Label, "Wait for shipping")
            .WithCorrelationId("$message.header.orderId")
            .WithTimeout(30_000)));
});

var app = builder.Build();

app.MapOpenApi();
app.MapAsyncApi();
app.MapArazzo();
app.MapScalarArazzoAssets();

// A hand-written Arazzo document that uses every field of the 1.1 specification (wwwroot/fixtures),
// so the Scalar view can be checked against all of them, not only what the workflow builder emits.
app.UseStaticFiles();

app.MapScalarApiReference(options => options
    .AddDocument("v1", "Orders")
    .AddDocument("payments", "Payment gateway")
    .AddDocument("inventory", "Inventory")
    .AddAsyncApiDocument("v1", "Events")
    .AddArazzoDocument("v1", "Workflows")
    .AddDocument("field-reference", "Arazzo field reference", "/fixtures/field-reference.arazzo.json")
    .WithArazzoOptions(arazzo =>
    {
        // Offers the "Technical | Pages" toggle on the workflow overview.
        arazzo.PagesExtension = ShopPages.Pages;
        arazzo.PageExtension = ShopPages.Page;
        arazzo.LabelExtension = ShopPages.Label;
        // The Sequence view: who makes a step's call, when it's a service rather than the client.
        arazzo.ActorExtension = Checkout.Actor;
        // ...and who is who: lane titles, their order, and the storefront as the client.
        arazzo.ActorsExtension = Checkout.Actors;
    }));

app.MapPost("/orders", (OrderRequest request) =>
        Results.Created($"/orders/{Guid.NewGuid()}", new Order(Guid.NewGuid(), request.Sku, request.Quantity, request.Quantity * 9.99m)))
    .WithName("createOrder")
    // The responses the workflows check for, so the Arazzo view can show them next to each step.
    .Produces<Order>(StatusCodes.Status201Created)
    .ProducesProblem(StatusCodes.Status400BadRequest);

app.MapGet("/orders/{id:guid}", (Guid id) => Results.Ok(new Order(id, "SKU-1", 1, 9.99m)))
    .WithName("getOrder")
    .Produces<Order>()
    .Produces(StatusCodes.Status404NotFound)
    .Produces(StatusCodes.Status410Gone);

app.MapCheckoutServices();

app.MapGet("/", () => Results.Redirect("/scalar")).ExcludeFromDescription();

app.Run();

/// <summary>The specification extensions the shop describes its pages with, for the Pages view.</summary>
internal static class ShopPages
{
    public const string Pages = "x-shop-pages";
    public const string Page = "x-page";
    public const string Label = "x-label";
}

internal sealed record PlaceOrderInputs(string Sku, int Quantity, ShippingAddress ShipTo);

internal sealed record ShippingAddress(string Street, string City, string PostalCode);

internal sealed record OrderRequest(string Sku, int Quantity);

internal sealed record Order(Guid Id, string Sku, int Quantity, decimal Total);

internal sealed record OrderPlaced(
    Guid Id,
    string Sku,
    int Quantity,
    IReadOnlyList<OrderLine> Lines,
    decimal Total,
    string Currency,
    ShippingAddress ShipTo,
    DateTimeOffset PlacedAt);

internal sealed record OrderLine(string Sku, string Name, int Quantity, decimal UnitPrice);

internal sealed record OrderShipped(Guid Id, string Carrier, string TrackingNumber, ShippingAddress ShipTo, DateTimeOffset ShippedAt);

internal sealed record PaymentRequested(Guid OrderId, decimal Amount, string Currency, string? CustomerReference);

// Sent as text, so the workflow's criteria can compare it with 'Succeeded'.
[JsonConverter(typeof(JsonStringEnumConverter<PaymentStatus>))]
internal enum PaymentStatus
{
    Succeeded,
    Declined,
    Pending,
}

internal sealed record PaymentCompleted(Guid OrderId, string PaymentId, PaymentStatus Status, decimal Amount, DateTimeOffset CompletedAt);

internal sealed record ReservedItem(string Sku, int Quantity, string Warehouse);

internal sealed record InventoryReserved(Guid OrderId, IReadOnlyList<ReservedItem> Items, DateTimeOffset ExpiresAt);

[AsyncApi]
internal sealed class OrderEvents
{
    [Channel("orderPlaced", Description = "Orders accepted by the shop.")]
    [Message(typeof(OrderPlaced), Title = "Order placed", Summary = "An order was accepted, with its lines, total and shipping address.", ContentType = "application/json")]
    [SendOperation(typeof(OrderPlaced), OperationId = "onOrderPlaced")]
    public void OrderPlacedEvent()
    {
    }

    [Channel("paymentRequested", Description = "Payment requests for the payment service.")]
    [Message(typeof(PaymentRequested), Title = "Payment requested", Summary = "Charge an order total.", ContentType = "application/json")]
    [SendOperation(typeof(PaymentRequested), OperationId = "requestPayment")]
    public void PaymentRequestedEvent()
    {
    }

    [Channel("paymentCompleted", Description = "Payment results, one per request.")]
    [Message(typeof(PaymentCompleted), Title = "Payment completed", Summary = "The outcome of a payment request.", ContentType = "application/json")]
    [ReceiveOperation(typeof(PaymentCompleted), OperationId = "onPaymentCompleted")]
    public void PaymentCompletedEvent()
    {
    }

    [Channel("inventoryReserved", Description = "Stock reservations made by the warehouse.")]
    [Message(typeof(InventoryReserved), Title = "Inventory reserved", Summary = "Stock was set aside for an order until it expires.", ContentType = "application/json")]
    [ReceiveOperation(typeof(InventoryReserved), OperationId = "onInventoryReserved")]
    public void InventoryReservedEvent()
    {
    }

    [Channel("orderShipped", Description = "Shipments handed to a carrier.")]
    [Message(typeof(OrderShipped), Title = "Order shipped", Summary = "An order left the warehouse.", ContentType = "application/json")]
    [ReceiveOperation(typeof(OrderShipped), OperationId = "onOrderShipped")]
    public void OrderShippedEvent()
    {
    }
}
