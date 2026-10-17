using Bielu.AspNetCore.Arazzo.Extensions;
using Bielu.AspNetCore.AsyncApi.Attributes.Attributes;
using Bielu.AspNetCore.AsyncApi.Extensions;

var builder = WebApplication.CreateBuilder(args);

// AddOpenApi() goes first: AddAsyncApi() composes with the OpenAPI document provider that is already registered,
// so one `dotnet getdocument` run writes every document. AddArazzo() may come before or after AddAsyncApi().
builder.Services.AddOpenApi();
builder.Services.AddAsyncApi();

// The Arazzo workflow references an OpenAPI operation and an AsyncAPI channel of this very app. They are checked
// when the app starts, which also happens when the build boots it to write the documents, so a stale reference
// fails the build. scripts/verify-build-time-documents.sh breaks the channel name on purpose to prove that.
var channelName = Environment.GetEnvironmentVariable("BUILD_TIME_DOCUMENTS_BREAK_ARAZZO") == "1"
    ? "doesNotExist"
    : "orderPlaced";

builder.Services.AddArazzo(options =>
{
    options.WithInfo("Order workflows", "1.0.0");
    options.AddOpenApiSource("orders", "v1");
    options.AddAsyncApiSource("events", "v1");
    options.AddWorkflow("placeOrder", workflow => workflow
        .Step("create", step => step.Operation("createOrder"))
        .Step("announce", step => step.Channel("events", channelName, Bielu.Arazzo.Models.ArazzoStepAction.Send)));
});

var app = builder.Build();

app.MapOpenApi();
app.MapAsyncApi();
app.MapArazzo();

app.MapPost("/orders", (OrderRequest request) => Results.Created($"/orders/{request.Sku}", request))
    .WithName("createOrder");

app.Run();

internal sealed record OrderRequest(string Sku, int Quantity);

internal sealed record OrderPlaced(string Sku, int Quantity);

[AsyncApi]
internal sealed class OrderEvents
{
    [Channel("orderPlaced")]
    [Message(typeof(OrderPlaced))]
    [SubscribeOperation(typeof(OrderPlaced), OperationId = "onOrderPlaced")]
    public void OrderPlacedEvent()
    {
    }
}
