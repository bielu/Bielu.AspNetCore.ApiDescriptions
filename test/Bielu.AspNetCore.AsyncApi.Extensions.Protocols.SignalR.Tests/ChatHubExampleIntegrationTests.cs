using System.Net;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Extensions.Protocols.SignalR.Tests;

/// <summary>
/// Boots the SignalRChat example application and verifies that the SignalR hub is mapped and that the
/// generated AsyncAPI document carries the SignalR protocol bindings.
/// </summary>
public class ChatHubExampleIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ChatHubExampleIntegrationTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task ChatHub_NegotiateEndpoint_IsMapped()
    {
        var client = _factory.CreateClient();

        // SignalR exposes a negotiate endpoint at <hub>/negotiate for the mapped hub.
        var response = await client.PostAsync("/chatHub/negotiate?negotiateVersion=1", content: null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("connectionId");
    }

    [Fact]
    public async Task AsyncApiDocument_ContainsSignalRBindings()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/asyncapi/signalr.json");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;

        // Server binding.
        json["servers"]!["signalr"]!["bindings"]!["signalr"]!["transports"]!
            .AsArray().Select(n => n!.GetValue<string>()).ShouldContain("webSockets");

        // Channel binding: the hub path.
        json["channels"]!["chatHub"]!["bindings"]!["signalr"]!["hub"]!
            .GetValue<string>().ShouldBe("/chatHub");

        // Operation binding: at least one operation targets the chat hub.
        var operations = json["operations"]!.AsObject();
        operations.Select(kvp => kvp.Value!["bindings"]?["signalr"]?["target"]?.GetValue<string>())
            .Where(t => t is not null)
            .ShouldNotBeEmpty();
    }
}
