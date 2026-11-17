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

    options.AddWorkflow("placeOrder", workflow => workflow
        .WithSummary("Place an order and announce it")
        .WithDescription(
            "Creates the order over HTTP, publishes `orderPlaced`, asks for payment, and waits for the payment and the stock reservation to come back as messages.")
        .WithInputs<PlaceOrderInputs>(new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        })
        .Step("create", step => step
            .Operation("createOrder")
            .WithDescription("Create the order.")
            .Payload(new JsonObject { ["sku"] = "$inputs.sku", ["quantity"] = "$inputs.quantity" }, "application/json")
            .SuccessCriteria("$statusCode == 201")
            .OnFailure(new ArazzoFailureAction
            {
                Name = "rejected",
                Type = "end",
                Criteria = [new ArazzoCriterion { Condition = "$statusCode == 400" }],
            })
            .Output("orderId", "$response.body#/id")
            .Output("total", "$response.body#/total"))
        .Step("announce", step => step
            .Channel("events", "orderPlaced", ArazzoStepAction.Send)
            .WithDescription("Tell the rest of the system about the new order.")
            .PayloadExpression("$steps.create.outputs")
            .DependsOn("create"))
        .Step("requestPayment", step => step
            .Channel("events", "paymentRequested", ArazzoStepAction.Send)
            .WithDescription("Ask the payment service to charge the order total.")
            .Payload(new JsonObject
            {
                ["orderId"] = "$steps.create.outputs.orderId",
                ["amount"] = "$steps.create.outputs.total",
                ["currency"] = "EUR",
            }, "application/json")
            .DependsOn("announce"))
        .Step("paymentCompleted", step => step
            .Channel("events", "paymentCompleted", ArazzoStepAction.Receive)
            .WithDescription("Wait for the payment result for this order.")
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
            })
            .Output("paymentId", "$message.payload.paymentId")
            .DependsOn("requestPayment"))
        .Step("stockReserved", step => step
            .Channel("events", "inventoryReserved", ArazzoStepAction.Receive)
            .WithDescription("Wait until the warehouse has reserved the stock.")
            .WithCorrelationId("$steps.create.outputs.orderId")
            .WithTimeout(120_000)
            .Output("reservationExpiresAt", "$message.payload.expiresAt")
            .DependsOn("paymentCompleted")));

    options.AddWorkflow("trackOrder", workflow => workflow
        .WithSummary("Follow an order to shipment")
        .Step("place", step => step
            .Workflow("placeOrder")
            .WithDescription("Reuse the placeOrder workflow."))
        .Step("status", step => step
            .Operation("getOrder")
            .WithDescription("Check the order status.")
            .Parameter("id", "$steps.place.outputs.orderId", "path")
            .SuccessCriteria("$statusCode == 200")
            // A JSONPath criterion (RFC 9535): only a live order counts as found.
            .SuccessCriteria(
                "$[?@.status != 'cancelled']",
                "$response.body",
                new ArazzoSelectorType { Type = "jsonpath", Version = "rfc9535" })
            // Freshly placed orders can take a moment to appear: retry a 404 a few times before giving up.
            .OnFailure(new ArazzoFailureAction
            {
                Name = "notYetVisible",
                Type = "retry",
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
            .OnFailure(new ArazzoFailureAction { Name = "unavailable", Type = "end" }))
        .Step("shipped", step => step
            .Channel("events", "orderShipped", ArazzoStepAction.Receive)
            .WithDescription("Wait for the shipment notification.")
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
    .AddAsyncApiDocument("v1", "Events")
    .AddArazzoDocument("v1", "Workflows")
    .AddDocument("field-reference", "Arazzo field reference", "/fixtures/field-reference.arazzo.json")
    .WithArazzo());

app.MapPost("/orders", (OrderRequest request) =>
        Results.Created($"/orders/{Guid.NewGuid()}", new Order(Guid.NewGuid(), request.Sku, request.Quantity, request.Quantity * 9.99m)))
    .WithName("createOrder");

app.MapGet("/orders/{id:guid}", (Guid id) => Results.Ok(new Order(id, "SKU-1", 1, 9.99m)))
    .WithName("getOrder");

app.MapGet("/", () => Results.Redirect("/scalar")).ExcludeFromDescription();

app.Run();

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
