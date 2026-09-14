using System.Net;
using System.Net.Http.Json;
using Bielu.AspNetCore.AsyncApi.Scalar.Broker.Tests.Fixtures;
using Microsoft.AspNetCore.TestHost;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Scalar.Broker.Tests.Integration;

/// <summary>
/// The proxy can publish to a broker, so who may reach it is the security-critical behaviour of
/// this package. These pin down every branch of that decision.
/// </summary>
public class BrokerBridgeAccessGuardTests
{
    [Fact]
    public async Task IsAllowed_DevelopmentWithoutAuthorization_ReturnsOk()
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
    public async Task IsAllowed_NonDevelopmentWithoutAuthorization_ReturnsForbidden(string environment)
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
    public async Task IsAllowed_NonDevelopmentWithoutAuthorization_RefusesPublishBeforeReachingTheBridge()
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
    public async Task IsAllowed_NonDevelopmentWithRequireAuthorization_ReturnsOk()
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
    public async Task IsAllowed_NonDevelopmentWithAllowAnonymousOption_ReturnsOk()
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
    public async Task IsAllowed_NonDevelopmentWithRequireAuthorizationAndAllowAnonymous_ReturnsForbidden()
    {
        // Arrange — RequireAuthorization() and AllowAnonymous() on the same endpoint is a
        // misconfiguration: the authorization middleware skips enforcement because AllowAnonymous
        // wins, so the guard must not treat IAuthorizeData alone as proof the endpoint is protected.
        using var host = await BrokerConsoleHost.StartAsync(
            new FakeBrokerBridge(),
            "Production",
            requireAuthorization: true,
            combineWithAllowAnonymous: true);

        // Act
        var response = await host.GetTestClient().GetAsync($"{BrokerConsoleHost.BasePath}/connections");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PluginBundle_NonDevelopmentWithRequireAuthorization_IsServedWithoutAuthorization()
    {
        // Arrange — the bundle is static JavaScript with no secrets, and is deliberately outside the
        // convention builder that RequireAuthorization is applied to.
        using var host = await BrokerConsoleHost.StartAsync(
            new FakeBrokerBridge(),
            "Production",
            requireAuthorization: true);

        // Act
        var response = await host.GetTestClient().GetAsync($"{BrokerConsoleHost.BasePath}/plugin.js");

        // Assert — the bundle build is what could 404 (Node-less build); the guard must never be why.
        response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
        response.StatusCode.ShouldNotBe(HttpStatusCode.Forbidden);
    }
}
