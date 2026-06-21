using System.Net;
using Bielu.AspNetCore.AsyncApi.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Tests.Integration;

/// <summary>
/// Verifies the AsyncAPI document endpoint surfaces serialization/generation failures as a non-200
/// status instead of returning HTTP 200 with a broken body (see issue #31).
/// </summary>
public class AsyncApiEndpointErrorHandlingTests
{
    private static async Task<IHost> CreateHostAsync(Action<Bielu.AspNetCore.AsyncApi.Services.AsyncApiOptions> configure)
    {
        var host = await Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    services.AddControllers();
                    services.AddAsyncApi(options =>
                    {
                        options.AddServer("test-server", "localhost", "http");
                        options.WithInfo("Test API", "1.0.0");
                        configure(options);
                    });
                    services.AddRouting();
                });
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapAsyncApi());
                });
            })
            .StartAsync();

        return host;
    }

    [Fact]
    public async Task DocumentGenerationFailure_Returns500_NotOk()
    {
        using var host = await CreateHostAsync(options =>
            options.AddDocumentTransformer((_, _, _) =>
                throw new InvalidOperationException("boom during document generation")));

        var client = host.GetTestClient();

        var response = await client.GetAsync("/asyncapi/v1.json");

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("AsyncApi document");
    }

    [Fact]
    public async Task ValidDocument_StillReturns200Json()
    {
        using var host = await CreateHostAsync(_ => { });

        var client = host.GetTestClient();

        var response = await client.GetAsync("/asyncapi/v1.json");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");
        response.Content.Headers.ContentLength.ShouldNotBeNull();
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("asyncapi");
    }
}
