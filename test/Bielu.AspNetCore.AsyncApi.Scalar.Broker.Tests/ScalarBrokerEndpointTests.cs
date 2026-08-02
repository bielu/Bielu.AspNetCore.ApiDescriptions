using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Scalar.Broker.Tests;

/// <summary>
/// The proxy endpoints <c>MapScalarBrokerAssets()</c> mounts: listing connections, publishing, and
/// tailing.
/// </summary>
public class ScalarBrokerEndpointTests
{
    [Fact]
    public async Task Connections_ReturnsRegisteredConnections()
    {
        // Arrange
        var bridge = new FakeBrokerBridge();
        using var host = await BrokerConsoleHost.StartAsync(bridge);

        // Act
        var response = await host.GetTestClient().GetAsync($"{BrokerConsoleHost.BasePath}/connections");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var connections = await response.Content.ReadFromJsonAsync<List<BrokerConnectionDescriptor>>();
        connections.ShouldNotBeNull();
        connections.Count.ShouldBe(1);
        connections[0].Name.ShouldBe("orders");
        connections[0].Protocol.ShouldBe("kafka");
        connections[0].Endpoint.ShouldBe("localhost:9092");
    }

    [Fact]
    public async Task Publish_ForwardsToBridgeAndReturnsReceipt()
    {
        // Arrange
        var bridge = new FakeBrokerBridge();
        using var host = await BrokerConsoleHost.StartAsync(bridge);

        // Act
        var response = await host.GetTestClient().PostAsJsonAsync(
            $"{BrokerConsoleHost.BasePath}/publish",
            new { connection = "orders", channel = "orders.created", payload = "{\"id\":1}", key = "k1" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var receipt = await response.Content.ReadFromJsonAsync<BrokerPublishReceipt>();
        receipt.ShouldNotBeNull();
        receipt.Channel.ShouldBe("orders.created");
        receipt.Partition.ShouldBe(3);
        receipt.Offset.ShouldBe(42);

        bridge.Published.Count.ShouldBe(1);
        bridge.Published[0].Channel.ShouldBe("orders.created");
        bridge.Published[0].Payload.ShouldBe("{\"id\":1}");
        bridge.Published[0].Key.ShouldBe("k1");
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"connection\":\"orders\"}")]
    [InlineData("{\"connection\":\"orders\",\"channel\":\"orders.created\"}")]
    public async Task Publish_MissingRequiredFields_Returns400(string body)
    {
        // Arrange
        var bridge = new FakeBrokerBridge();
        using var host = await BrokerConsoleHost.StartAsync(bridge);

        // Act
        var response = await host.GetTestClient().PostAsync(
            $"{BrokerConsoleHost.BasePath}/publish",
            new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        bridge.Published.ShouldBeEmpty();
    }

    [Fact]
    public async Task Publish_UnknownConnection_Returns404NamingTheKnownOnes()
    {
        // Arrange
        var bridge = new FakeBrokerBridge();
        using var host = await BrokerConsoleHost.StartAsync(bridge);

        // Act
        var response = await host.GetTestClient().PostAsJsonAsync(
            $"{BrokerConsoleHost.BasePath}/publish",
            new { connection = "typo", channel = "orders.created", payload = "{}" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var problem = await response.Content.ReadAsStringAsync();
        problem.ShouldContain("typo");
        // The list of registered names is what makes the 404 actionable rather than a dead end.
        problem.ShouldContain("orders");
        bridge.Published.ShouldBeEmpty();
    }

    [Fact]
    public async Task Tail_StreamsMessagesAsServerSentEvents()
    {
        // Arrange
        var bridge = new FakeBrokerBridge();
        using var host = await BrokerConsoleHost.StartAsync(bridge);
        bridge.Emit(new BrokerMessage(
            "orders.created",
            "k1",
            new Dictionary<string, string> { ["trace"] = "abc" },
            "{\"id\":1}",
            new DateTimeOffset(2026, 8, 2, 10, 0, 0, TimeSpan.Zero),
            Partition: 3,
            Offset: 42));
        bridge.CompleteTail();

        // Act
        var response = await host.GetTestClient().GetAsync(
            $"{BrokerConsoleHost.BasePath}/tail?connection=orders&channel=orders.created",
            HttpCompletionOption.ResponseHeadersRead);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/event-stream");

        var body = await response.Content.ReadAsStringAsync();
        body.ShouldStartWith("data: ");
        body.ShouldEndWith("\n\n");

        var payload = JsonDocument.Parse(body["data: ".Length..].Trim()).RootElement;
        payload.GetProperty("channel").GetString().ShouldBe("orders.created");
        payload.GetProperty("key").GetString().ShouldBe("k1");
        payload.GetProperty("payload").GetString().ShouldBe("{\"id\":1}");
        payload.GetProperty("offset").GetInt64().ShouldBe(42);
        payload.GetProperty("headers").GetProperty("trace").GetString().ShouldBe("abc");
    }

    [Theory]
    [InlineData("?channel=orders.created")]
    [InlineData("?connection=orders")]
    [InlineData("")]
    public async Task Tail_MissingQueryParameters_Returns400(string query)
    {
        // Arrange
        using var host = await BrokerConsoleHost.StartAsync(new FakeBrokerBridge());

        // Act
        var response = await host.GetTestClient().GetAsync($"{BrokerConsoleHost.BasePath}/tail{query}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Connections_WithNoRegisteredConnections_ReturnsEmptyArray()
    {
        // Arrange — the app called AddScalarBrokerBridge but registered no driver connection.
        using var host = await BrokerConsoleHost.StartAsync();

        // Act
        var response = await host.GetTestClient().GetAsync($"{BrokerConsoleHost.BasePath}/connections");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<List<BrokerConnectionDescriptor>>()).ShouldBeEmpty();
    }
}
