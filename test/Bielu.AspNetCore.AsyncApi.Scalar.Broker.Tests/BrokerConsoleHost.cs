using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bielu.AspNetCore.AsyncApi.Scalar.Broker.Tests;

/// <summary>
/// Builds a <see cref="TestServer" /> hosting the broker console endpoints over a fake bridge.
/// </summary>
internal static class BrokerConsoleHost
{
    public const string BasePath = "/bielu/scalar/broker";

    /// <summary>The scheme name of the always-succeeds authentication used by the secured host.</summary>
    public const string TestScheme = "Test";

    /// <summary>
    /// Starts a host with the console mapped.
    /// </summary>
    /// <param name="bridge">The bridge backing the single registered connection, or none.</param>
    /// <param name="environment">The hosting environment name; drives the access guard.</param>
    /// <param name="allowAnonymous">Sets <see cref="ScalarBrokerBridgeOptions.AllowAnonymous" />.</param>
    /// <param name="requireAuthorization">Applies <c>RequireAuthorization()</c> to the proxy endpoints.</param>
    public static Task<IHost> StartAsync(
        FakeBrokerBridge? bridge = null,
        // Literal rather than Environments.Development: that is a static readonly field, not a const.
        string environment = "Development",
        bool allowAnonymous = false,
        bool requireAuthorization = false)
    {
        return new HostBuilder()
            .UseEnvironment(environment)
            .ConfigureWebHost(webHost => webHost
                .UseTestServer()
                .ConfigureLogging(logging => logging.ClearProviders())
                .ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddAuthorization();
                    services.AddAuthentication(TestScheme)
                        .AddScheme<AuthenticationSchemeOptions, AlwaysAuthenticatedHandler>(TestScheme, _ => { });

                    services.AddScalarBrokerBridge(options =>
                    {
                        options.AllowAnonymous = allowAnonymous;
                        if (bridge is not null)
                        {
                            options.AddConnection(new BrokerConnectionRegistration(
                                new BrokerConnectionDescriptor("orders", "kafka", "localhost:9092"),
                                _ => bridge));
                        }
                    });
                })
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints =>
                    {
                        var console = endpoints.MapScalarBrokerAssets();
                        if (requireAuthorization)
                        {
                            console.RequireAuthorization();
                        }
                    });
                }))
            .StartAsync();
    }

    /// <summary>Authenticates every request, so an authorized endpoint can be reached in a test.</summary>
    private sealed class AlwaysAuthenticatedHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "tester")], TestScheme);
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), TestScheme);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
