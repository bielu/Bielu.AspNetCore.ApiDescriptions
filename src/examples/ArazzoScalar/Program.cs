using System.Text.Json;
using System.Text.Json.Nodes;
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
        .WithDescription("Creates the order over HTTP, then publishes `orderPlaced` so fulfilment can start.")
        .WithInputs<PlaceOrderInputs>(new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        })
        .Step("create", step => step
            .Operation("createOrder")
            .WithDescription("Create the order.")
            .Payload(new JsonObject { ["sku"] = "$inputs.sku", ["quantity"] = "$inputs.quantity" }, "application/json")
            .SuccessCriteria("$statusCode == 201")
            .Output("orderId", "$response.body#/id"))
        .Step("announce", step => step
            .Channel("events", "orderPlaced", ArazzoStepAction.Send)
            .WithDescription("Tell the rest of the system about the new order.")
            .PayloadExpression("$steps.create.outputs")
            .DependsOn("create")));

    options.AddWorkflow("trackOrder", workflow => workflow
        .WithSummary("Follow an order to shipment")
        .Step("place", step => step
            .Workflow("placeOrder")
            .WithDescription("Reuse the placeOrder workflow."))
        .Step("status", step => step
            .Operation("getOrder")
            .Parameter("id", "$steps.place.outputs.orderId", "path")
            .SuccessCriteria("$statusCode == 200"))
        .Step("shipped", step => step
            .Channel("events", "orderShipped", ArazzoStepAction.Receive)
            .WithDescription("Wait for the shipment notification.")
            .WithTimeout(30_000)));
});

var app = builder.Build();

app.MapOpenApi();
app.MapAsyncApi();
app.MapArazzo();
app.MapScalarArazzoAssets();

app.MapScalarApiReference(options => options
    .AddDocument("v1", "Orders")
    .AddAsyncApiDocument("v1", "Events")
    .AddArazzoDocument("v1", "Workflows")
    .WithArazzo());

app.MapPost("/orders", (OrderRequest request) =>
        Results.Created($"/orders/{Guid.NewGuid()}", new Order(Guid.NewGuid(), request.Sku, request.Quantity)))
    .WithName("createOrder");

app.MapGet("/orders/{id:guid}", (Guid id) => Results.Ok(new Order(id, "SKU-1", 1)))
    .WithName("getOrder");

app.MapGet("/", () => Results.Redirect("/scalar")).ExcludeFromDescription();

app.Run();

internal sealed record PlaceOrderInputs(string Sku, int Quantity);

internal sealed record OrderRequest(string Sku, int Quantity);

internal sealed record Order(Guid Id, string Sku, int Quantity);

internal sealed record OrderPlaced(Guid Id, string Sku, int Quantity);

internal sealed record OrderShipped(Guid Id, DateTimeOffset ShippedAt);

[AsyncApi]
internal sealed class OrderEvents
{
    [Channel("orderPlaced")]
    [Message(typeof(OrderPlaced))]
    [SendOperation(typeof(OrderPlaced), OperationId = "onOrderPlaced")]
    public void OrderPlacedEvent()
    {
    }

    [Channel("orderShipped")]
    [Message(typeof(OrderShipped))]
    [ReceiveOperation(typeof(OrderShipped), OperationId = "onOrderShipped")]
    public void OrderShippedEvent()
    {
    }
}
