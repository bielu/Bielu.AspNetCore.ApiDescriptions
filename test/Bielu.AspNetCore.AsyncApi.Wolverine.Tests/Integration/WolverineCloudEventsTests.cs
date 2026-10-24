using System.Text.Json.Nodes;
using Bielu.AspNetCore.AsyncApi.CloudEvents;
using Bielu.AspNetCore.AsyncApi.Wolverine.Tests.Fixtures;
using Shouldly;
using Wolverine.Kafka;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Wolverine.Tests.Integration;

/// <summary>
/// <c>DescribeCloudEvents()</c> must describe exactly what Wolverine's <c>CloudEventsMapper</c> writes, and only on
/// the endpoints that really send CloudEvents.
/// </summary>
public class WolverineCloudEventsTests
{
    private const string ServiceName = "orders-service";

    [Fact]
    public async Task DescribeCloudEvents_SignalR_DescribesTheEnvelopeWolverineWrites()
    {
        // Act
        var document = await GetDocumentAsync(wolverine => wolverine.IncludeSchemes("signalr").DescribeCloudEvents());

        // Assert
        var message = Message(document, "orders.order-shipped");
        message["contentType"]!.GetValue<string>().ShouldBe("application/cloudevents+json");
        var properties = message["payload"]!["properties"]!;
        properties["type"]!["const"]!.GetValue<string>().ShouldBe("orders.order-shipped");
        properties["source"]!["const"]!.GetValue<string>().ShouldBe(ServiceName);
        properties["specversion"]!["const"]!.GetValue<string>().ShouldBe("1.0");
        properties["datacontenttype"]!["const"]!.GetValue<string>().ShouldBe("application/json; charset=utf-8");
        properties["data"]!["$ref"]!.GetValue<string>().ShouldBe("#/components/schemas/orderShipped");
        properties.AsObject().Select(p => p.Key).ShouldContain("traceparent");
        properties.AsObject().Select(p => p.Key).ShouldContain("tenantid");
    }

    [Fact]
    public async Task DescribeCloudEvents_Omitted_LeavesSignalRMessagesPlain()
    {
        // Act
        var document = await GetDocumentAsync(wolverine => wolverine.IncludeSchemes("signalr"));

        // Assert
        var message = Message(document, "orders.order-shipped");
        message["payload"]!["$ref"]!.GetValue<string>().ShouldBe("#/components/schemas/orderShipped");
        message["contentType"].ShouldBeNull();
    }

    [Fact]
    public async Task DescribeCloudEvents_PlainKafkaEndpoint_IsLeftAlone()
    {
        // Act
        var document = await GetDocumentAsync(wolverine => wolverine.IncludeSchemes("kafka").DescribeCloudEvents());

        // Assert
        Message(document, "orders.order-placed")["payload"]!["$ref"]!.GetValue<string>()
            .ShouldBe("#/components/schemas/orderPlaced");
    }

    [Fact]
    public async Task DescribeCloudEvents_KafkaEndpointWithInteropWithCloudEvents_IsDetected()
    {
        // Act
        var document = await GetDocumentAsync(
            wolverine => wolverine.IncludeSchemes("kafka").DescribeCloudEvents(),
            opts => opts.PublishMessage<Quote>().ToKafkaTopic("quotes").InteropWithCloudEvents());

        // Assert
        var quote = Message(document, "quotes.quote");
        quote["contentType"]!.GetValue<string>().ShouldBe("application/cloudevents+json");
        quote["payload"]!["properties"]!["type"]!["const"]!.GetValue<string>().ShouldBe("quotes.quote");
        Message(document, "orders.order-placed")["contentType"].ShouldBeNull();
    }

    [Fact]
    public async Task DescribeCloudEvents_ListedEndpoint_IsDescribed()
    {
        // Act
        var document = await GetDocumentAsync(wolverine => wolverine
            .IncludeSchemes("kafka")
            .DescribeCloudEvents(new Uri("kafka://topic/orders")));

        // Assert
        Message(document, "orders.order-placed")["contentType"]!.GetValue<string>()
            .ShouldBe("application/cloudevents+json");
    }

    [Fact]
    public async Task DescribeCloudEvents_MessageAlsoOnPlainEndpoint_GetsACopyForTheCloudEventsChannel()
    {
        // Act
        var document = await GetDocumentAsync(
            wolverine => wolverine.DescribeCloudEvents(),
            opts => opts.PublishMessage<OrderShipped>().ToKafkaTopic("shipments"));

        // Assert
        Message(document, "orders.order-shipped")["payload"]!["$ref"]!.GetValue<string>()
            .ShouldBe("#/components/schemas/orderShipped");
        Message(document, "orders.order-shipped.cloudevent")["contentType"]!.GetValue<string>()
            .ShouldBe("application/cloudevents+json");

        var channels = document["channels"]!;
        channels["hubstest"]!["messages"]!["orders.order-shipped"]!["$ref"]!.GetValue<string>()
            .ShouldBe("#/components/messages/orders.order-shipped.cloudevent");
        channels["shipments"]!["messages"]!["orders.order-shipped"]!["$ref"]!.GetValue<string>()
            .ShouldBe("#/components/messages/orders.order-shipped");

        // The operations still point at the channel's message, under its unchanged key.
        document["operations"]!.AsObject()
            .SelectMany(operation => operation.Value!["messages"]!.AsArray())
            .Select(reference => reference!["$ref"]!.GetValue<string>())
            .ShouldContain("#/channels/hubstest/messages/orders.order-shipped");
    }

    [Fact]
    public async Task AddCloudEvents_AfterDescribeCloudEvents_DoesNotWrapTheEnvelopeAgain()
    {
        // Act
        var document = await WolverineTestApp.GetDocumentAsync("cloudevents-combined", options =>
        {
            options.AddServer("signalr", "localhost", "signalr");
            options.AddWolverine(wolverine => wolverine.IncludeSchemes("signalr").DescribeCloudEvents());
            options.AddCloudEvents(cloudEvents => cloudEvents.UseMode(CloudEventsMode.Structured));
        }, configureWolverine: opts => opts.ServiceName = ServiceName);

        // Assert
        var properties = Message(document, "orders.order-shipped")["payload"]!["properties"]!;
        properties["data"]!["$ref"]!.GetValue<string>().ShouldBe("#/components/schemas/orderShipped");
        properties["source"]!["const"]!.GetValue<string>().ShouldBe(ServiceName);
    }

    [Fact]
    public void DescribeCloudEvents_ThrowsForNullEndpoints()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new WolverineAsyncApiOptions().DescribeCloudEvents(null!));
        Should.Throw<ArgumentNullException>(() => new WolverineAsyncApiOptions().DescribeCloudEvents([null!]));
    }

    [Fact]
    public void DescribeCloudEvents_RecordsTheEndpoints()
    {
        // Arrange
        var options = new WolverineAsyncApiOptions();

        // Act
        var result = options.DescribeCloudEvents(new Uri("kafka://topic/orders"));

        // Assert
        result.ShouldBeSameAs(options);
        options.CloudEventsDescribed.ShouldBeTrue();
        options.CloudEventsEndpoints.ShouldBe([new Uri("kafka://topic/orders")]);
    }

    private static JsonNode Message(JsonNode document, string key) => document["components"]!["messages"]![key]!;

    private static Task<JsonNode> GetDocumentAsync(Action<WolverineAsyncApiOptions> configure,
        Action<global::Wolverine.WolverineOptions>? configureWolverine = null) =>
        WolverineTestApp.GetDocumentAsync("cloudevents", options =>
        {
            options.AddServer("signalr", "localhost", "signalr");
            options.AddServer("kafka", "localhost:9092", "kafka");
            options.AddWolverine(configure);
        }, configureWolverine: opts =>
        {
            opts.ServiceName = ServiceName;
            configureWolverine?.Invoke(opts);
        });
}
