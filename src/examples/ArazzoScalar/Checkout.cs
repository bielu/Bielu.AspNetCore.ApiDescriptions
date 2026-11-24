using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Bielu.Arazzo.Models;
using Bielu.AspNetCore.Arazzo.Services;
using Bielu.AspNetCore.AsyncApi.Attributes;

/// <summary>
/// A checkout spread over several services: the client talks to the orders service, which reserves stock with the
/// inventory service, has the payment gateway authorize and capture the payment, and announces the confirmed order.
/// </summary>
/// <remarks>
/// Each service has an OpenAPI document of its own (this one app serves them all, as a stand-in for separate
/// services), and each is a source description of the Arazzo document, so each gets a lane in the Sequence view.
/// <c>checkout</c> is made of three workflows, one per stage; two of them run at the same time (<c>dependsOn</c>),
/// and their steps' failure actions show as the ways each step can end. The services' calls to each other are
/// marked with <c>x-actor</c> (Arazzo has no field for who makes a call, and without it every arrow would start at
/// the client), and <c>x-actors</c> titles the lanes and names the storefront as the client.
/// </remarks>
internal static class Checkout
{
    /// <summary>The extension naming who makes a step's call; <c>ActorExtension</c> on the Scalar side.</summary>
    public const string Actor = "x-actor";

    /// <summary>The document's actor registry: lane titles, their order, and the client; <c>ActorsExtension</c>.</summary>
    public const string Actors = "x-actors";

    /// <summary>How the workflows' inputs schemas are generated from their records.</summary>
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
    };

    /// <summary>Adds the payment gateway's and the inventory service's OpenAPI documents.</summary>
    public static IServiceCollection AddCheckoutServices(this IServiceCollection services)
    {
        services.AddOpenApi("payments", options => options.AddDocumentTransformer((document, _, _) =>
        {
            document.Info.Title = "Payment gateway";
            return Task.CompletedTask;
        }));
        services.AddOpenApi("inventory", options => options.AddDocumentTransformer((document, _, _) =>
        {
            document.Info.Title = "Inventory";
            return Task.CompletedTask;
        }));
        return services;
    }

    /// <summary>Adds the services as sources, and the checkout workflow with its three stages.</summary>
    public static ArazzoOptions AddCheckoutWorkflows(this ArazzoOptions options)
    {
        options.AddOpenApiSource("payments", "payments");
        options.AddOpenApiSource("inventory", "inventory");

        // Who is who in the Sequence view: the storefront runs the workflows (the client), and each service's lane
        // gets a title, in this order. The names are the source descriptions' (orders, inventory, payments, events).
        options.WithExtension(Actors, new JsonObject
        {
            ["storefront"] = new JsonObject { ["title"] = "Storefront", ["description"] = "The shop the customer checks out in.", ["client"] = true },
            ["orders"] = new JsonObject { ["title"] = "Orders service" },
            ["inventory"] = new JsonObject { ["title"] = "Inventory service" },
            ["payments"] = new JsonObject { ["title"] = "Payment gateway", ["description"] = "Authorizes and captures card payments." },
            ["events"] = new JsonObject { ["title"] = "Message broker" },
            ["updates"] = new JsonObject { ["title"] = "Order updates", ["description"] = "The SignalR hub the storefront's pages listen on." },
            ["email"] = new JsonObject { ["title"] = "Email service" },
        });

        // Stage 1. The orders service makes every call of the three stages: the workflow says so once.
        options.AddWorkflow("reserveStock", workflow => workflow
            .WithSummary("Reserve the stock")
            .WithDescription("The orders service sets the stock aside before anyone pays for it.")
            .WithExtension(Actor, "orders")
            .WithInputs<CheckoutOrder>(Json)
            .Step("reserve", step => step
                .Operation("createReservation")
                .WithDescription("Ask the inventory service to hold the order's items.")
                .Payload(new JsonObject { ["orderId"] = null }, "application/json")
                .Replacement("/orderId", "$inputs.orderId")
                .SuccessCriteria("$statusCode == 201")
                .Output("reservationId", "$response.body#/id"))
            .Output("reservationId", "$steps.reserve.outputs.reservationId"));

        // Stage 2: the payment gateway authorizes the payment, and reports the result as a message.
        options.AddWorkflow("takePayment", workflow => workflow
            .WithSummary("Take the payment")
            .WithDescription("The orders service has the payment gateway authorize the order total.")
            .WithExtension(Actor, "orders")
            .WithInputs<CheckoutOrder>(Json)
            .Step("authorize", step => step
                .Operation("authorizePayment")
                .WithDescription("Ask the payment gateway to authorize the payment.")
                .Payload(new JsonObject { ["orderId"] = null }, "application/json")
                .Replacement("/orderId", "$inputs.orderId")
                .SuccessCriteria("$statusCode == 202")
                // The gateway refuses outright when the card can't be charged at all.
                .OnFailure(new ArazzoFailureAction
                {
                    Name = "refused",
                    Type = "end",
                    Criteria = [new ArazzoCriterion { Condition = "$statusCode == 402" }],
                })
                .Output("paymentId", "$response.body#/id"))
            .Step("authorized", step => step
                .Channel("events", "paymentCompleted", ArazzoStepAction.Receive)
                .WithDescription("Wait for the payment gateway's answer.")
                .WithCorrelationId("$inputs.orderId")
                .WithTimeout(60_000)
                .SuccessCriteria("$message.payload#/status == 'Succeeded'")
                .OnFailure(new ArazzoFailureAction
                {
                    Name = "declined",
                    Type = "end",
                    Criteria = [new ArazzoCriterion { Condition = "$message.payload#/status == 'Declined'" }],
                })
                .DependsOn("authorize"))
            .Output("paymentId", "$steps.authorize.outputs.paymentId"));

        // Stage 3: capture the money and commit the stock (at the same time: neither waits on the other), then tell
        // everyone.
        options.AddWorkflow("confirmOrder", workflow => workflow
            .WithSummary("Confirm the order")
            .WithDescription("The orders service captures the payment, commits the reservation and announces the order.")
            .WithExtension(Actor, "orders")
            .WithInputs<ConfirmOrderInputs>(Json)
            .Step("capture", step => step
                .Operation("capturePayment")
                .WithDescription("Capture the authorized payment.")
                .Parameter("id", "$inputs.paymentId", ArazzoParameterLocation.Path)
                .SuccessCriteria("$statusCode == 200"))
            .Step("commit", step => step
                .Operation("commitReservation")
                .WithDescription("Turn the reservation into a shipment.")
                .Parameter("id", "$inputs.reservationId", ArazzoParameterLocation.Path)
                .SuccessCriteria("$statusCode == 200"))
            .Step("announce", step => step
                .Channel("events", "orderConfirmed", ArazzoStepAction.Send)
                .WithDescription("Tell the rest of the system the order is confirmed.")
                .DependsOn("capture", "commit")));

        // The storefront's view: it places the order and checks on it; everything in between is the services'.
        // Reserving the stock and taking the payment both wait only for the order, so they run at the same time.
        options.AddWorkflow("checkout", workflow => workflow
            .WithSummary("Check out across services")
            .WithDescription(
                "The storefront places an order and later checks on it. In between, the orders service reserves the stock and takes the payment at the same time, then confirms the order: open the Sequence view to see who calls whom.")
            .Step("create", step => step
                .Operation("createOrder")
                .WithDescription("Place the order.")
                .Payload(new JsonObject { ["sku"] = "SKU-1", ["quantity"] = 1 }, "application/json")
                .SuccessCriteria("$statusCode == 201")
                .OnFailure(new ArazzoFailureAction
                {
                    Name = "rejected",
                    Type = "end",
                    Criteria = [new ArazzoCriterion { Condition = "$statusCode == 400" }],
                })
                .Output("orderId", "$response.body#/id"))
            .Step("stock", step => step
                .Workflow("reserveStock")
                .Parameter("orderId", "$steps.create.outputs.orderId")
                .Output("reservationId", "$workflows.reserveStock.outputs.reservationId")
                .DependsOn("create"))
            .Step("payment", step => step
                .Workflow("takePayment")
                .Parameter("orderId", "$steps.create.outputs.orderId")
                .Output("paymentId", "$workflows.takePayment.outputs.paymentId")
                .DependsOn("create"))
            .Step("confirm", step => step
                .Workflow("confirmOrder")
                .Parameter("paymentId", "$steps.payment.outputs.paymentId")
                .Parameter("reservationId", "$steps.stock.outputs.reservationId")
                .DependsOn("stock", "payment"))
            .Step("status", step => step
                .Operation("getOrder")
                .WithDescription("Check the order.")
                .Parameter("id", "$steps.create.outputs.orderId", ArazzoParameterLocation.Path)
                .SuccessCriteria("$statusCode == 200")
                .DependsOn("confirm")));

        return options;
    }

    /// <summary>The payment gateway's and the inventory service's endpoints.</summary>
    public static IEndpointRouteBuilder MapCheckoutServices(this IEndpointRouteBuilder app)
    {
        var payments = app.MapGroup("/payments").WithGroupName("payments");
        payments.MapPost("/", (PaymentRequest request) =>
                Results.Accepted($"/payments/{Guid.NewGuid()}", new Payment(Guid.NewGuid(), request.OrderId, "Authorizing")))
            .WithName("authorizePayment")
            .Produces<Payment>(StatusCodes.Status202Accepted);
        payments.MapPost("/{id:guid}/capture", (Guid id) => Results.Ok(new Payment(id, Guid.Empty, "Captured")))
            .WithName("capturePayment")
            .Produces<Payment>();

        var inventory = app.MapGroup("/reservations").WithGroupName("inventory");
        inventory.MapPost("/", (ReservationRequest request) =>
                Results.Created($"/reservations/{Guid.NewGuid()}", new Reservation(Guid.NewGuid(), request.OrderId, "Held")))
            .WithName("createReservation")
            .Produces<Reservation>(StatusCodes.Status201Created);
        inventory.MapPost("/{id:guid}/commit", (Guid id) => Results.Ok(new Reservation(id, Guid.Empty, "Committed")))
            .WithName("commitReservation")
            .Produces<Reservation>();
        return app;
    }
}

internal sealed record CheckoutOrder(Guid OrderId);

internal sealed record ConfirmOrderInputs(Guid PaymentId, Guid ReservationId);

internal sealed record PaymentRequest(Guid OrderId);

internal sealed record Payment(Guid Id, Guid OrderId, string Status);

internal sealed record ReservationRequest(Guid OrderId);

internal sealed record Reservation(Guid Id, Guid OrderId, string Status);

internal sealed record OrderConfirmed(Guid OrderId, Guid PaymentId, DateTimeOffset ConfirmedAt);

[AsyncApi]
internal sealed class CheckoutEvents
{
    [Channel("orderConfirmed", Description = "Orders paid for and committed.")]
    [Message(typeof(OrderConfirmed), Title = "Order confirmed", Summary = "An order was paid for and its stock committed.", ContentType = "application/json")]
    [SendOperation(typeof(OrderConfirmed), OperationId = "onOrderConfirmed")]
    public void OrderConfirmedEvent()
    {
    }
}
