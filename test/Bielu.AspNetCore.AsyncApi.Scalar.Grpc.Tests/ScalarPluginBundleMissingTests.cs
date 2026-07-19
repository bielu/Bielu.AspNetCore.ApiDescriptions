using System.Net;
using System.Reflection;
using Bielu.AspNetCore.AsyncApi.Scalar;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Scalar.Grpc.Tests;

/// <summary>
/// Verifies the shared bundle endpoint's behaviour when the embedded resource is absent — the
/// state a .NET-only build (no Node) ends up in.
/// </summary>
public class ScalarPluginBundleMissingTests
{
    private const string MissingMessage = "Scalar gRPC bundle was not embedded. Build the assets npm package (npm run build).";

    [Fact]
    public async Task MapScalarPluginBundle_MissingBundle_Returns404WithActionableMessage()
    {
        // Arrange
        using var host = await new HostBuilder()
            .ConfigureWebHost(webHost => webHost
                .UseTestServer()
                .ConfigureServices(services => services.AddRouting())
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapScalarPluginBundle(
                        "/bielu/scalar/grpc",
                        Assembly.GetExecutingAssembly(),
                        "Does.Not.Exist.plugin.js",
                        MissingMessage));
                }))
            .StartAsync();

        // Act
        var client = host.GetTestClient();
        var response = await client.GetAsync("/bielu/scalar/grpc/plugin.js");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).ShouldBe(MissingMessage);
    }
}
