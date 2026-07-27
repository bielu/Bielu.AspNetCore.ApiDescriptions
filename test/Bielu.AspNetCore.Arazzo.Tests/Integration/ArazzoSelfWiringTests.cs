using Bielu.AspNetCore.Arazzo.Extensions;
using Bielu.AspNetCore.Arazzo.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.Arazzo.Tests.Integration;

/// <summary>Startup cross-spec validation of self-wired OpenAPI sources (see ArazzoOptions.AddOpenApiSource).</summary>
public class ArazzoSelfWiringTests
{
    [Fact]
    public async Task Startup_ResolvableOperationId_Succeeds()
    {
        using var host = await BuildHostAsync(operationId: "createOrder");

        // Reaching here means ArazzoStartupValidationHostedService resolved the step's operationId
        // against the live OpenAPI document without throwing.
        host.ShouldNotBeNull();
    }

    [Fact]
    public async Task Startup_UnresolvableOperationId_ThrowsAggregatingTheFailure()
    {
        var exception = await Should.ThrowAsync<ArazzoStartupValidationException>(() => BuildHostAsync(operationId: "doesNotExist"));

        exception.Errors.ShouldHaveSingleItem();
        exception.Errors[0].ShouldContain("doesNotExist");
    }

    private static async Task<IHost> BuildHostAsync(string operationId)
    {
        var builder = Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddOpenApi("v1", openApiOptions => openApiOptions.AddOperationTransformer((operation, _, _) =>
                    {
                        operation.OperationId = "createOrder";
                        return Task.CompletedTask;
                    }));
                    services.AddArazzo(options =>
                    {
                        options.WithInfo("Test Workflows", "1.0.0");
                        options.AddOpenApiSource("orders", "v1");
                        options.AddWorkflow("createOrder", wf => wf
                            .Step("create", s => s.Operation(operationId)));
                    });
                    services.AddRouting();
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGet("/orders", () => "ok").WithName("createOrder");
                        endpoints.MapOpenApi();
                        endpoints.MapArazzo();
                    });
                });
            });

        var host = builder.Build();
        await host.StartAsync();
        return host;
    }
}
