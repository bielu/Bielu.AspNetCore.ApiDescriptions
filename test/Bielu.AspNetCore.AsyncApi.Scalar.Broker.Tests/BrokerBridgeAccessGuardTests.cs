using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Scalar.Broker.Tests;

/// <summary>
/// The proxy can publish to a broker, so who may reach it is the security-critical behaviour of
/// this package. These pin down every branch of that decision.
/// </summary>
public class BrokerBridgeAccessGuardTests
{
    [Fact]
    public async Task Development_WithoutAuthorization_IsAllowed()
    {
        // Arrange — the local-development case: convenient, and warned about in the log.
        using var host = await BrokerConsoleHost.StartAsync(new FakeBrokerBridge(), "Development");

        // Act
        var response = await host.GetTestClient().GetAsync($"{BrokerConsoleHost.BasePath}/connections");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task NonDevelopment_WithoutAuthorization_IsRefused(string environment)
    {
        // Arrange
        var bridge = new FakeBrokerBridge();
        using var host = await BrokerConsoleHost.StartAsync(bridge, environment);

        // Act
        var response = await host.GetTestClient().GetAsync($"{BrokerConsoleHost.BasePath}/connections");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task NonDevelopment_WithoutAuthorization_RefusesPublishBeforeReachingTheBridge()
    {
        // Arrange — a refusal that still published would be worse than no guard at all.
        var bridge = new FakeBrokerBridge();
        using var host = await BrokerConsoleHost.StartAsync(bridge, "Production");

        // Act
        var response = await host.GetTestClient().PostAsJsonAsync(
            $"{BrokerConsoleHost.BasePath}/publish",
            new { connection = "orders", channel = "orders.created", payload = "{}" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        bridge.Published.ShouldBeEmpty();
    }

    [Fact]
    public async Task NonDevelopment_WithRequireAuthorization_IsAllowed()
    {
        // Arrange — the intended production shape.
        using var host = await BrokerConsoleHost.StartAsync(
            new FakeBrokerBridge(),
            "Production",
            requireAuthorization: true);

        // Act
        var response = await host.GetTestClient().GetAsync($"{BrokerConsoleHost.BasePath}/connections");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task NonDevelopment_WithAllowAnonymous_IsAllowed()
    {
        // Arrange — the explicit opt-out, for endpoints fronted by something outside ASP.NET Core.
        using var host = await BrokerConsoleHost.StartAsync(
            new FakeBrokerBridge(),
            "Production",
            allowAnonymous: true);

        // Act
        var response = await host.GetTestClient().GetAsync($"{BrokerConsoleHost.BasePath}/connections");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task BundleIsServedWithoutAuthorization()
    {
        // Arrange — the bundle is static JavaScript with no secrets, and is deliberately outside the
        // convention builder that RequireAuthorization is applied to.
        using var host = await BrokerConsoleHost.StartAsync(
            new FakeBrokerBridge(),
            "Production",
            requireAuthorization: true);

        // Act
        var response = await host.GetTestClient().GetAsync($"{BrokerConsoleHost.BasePath}/plugin.js");

        // Assert — 200 with the embedded bundle, or 404 when built without Node; never a 401/403.
        response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }
}
