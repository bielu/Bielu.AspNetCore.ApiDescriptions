using System.Text.Json.Nodes;
using Bielu.Arazzo;
using Bielu.Arazzo.Models;
using Bielu.AspNetCore.Arazzo.Extensions;
using Bielu.AspNetCore.Arazzo.Validation;
using Bielu.AspNetCore.AsyncApi.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.Arazzo.Tests.Integration;

/// <summary>Startup cross-spec validation of self-wired OpenAPI/AsyncAPI sources (see ArazzoOptions.AddOpenApiSource/AddAsyncApiSource).</summary>
public class ArazzoSelfWiringTests
{
    [Fact]
    public async Task Startup_ResolvableOperationId_Succeeds()
    {
        using var host = await BuildOperationIdHostAsync(operationId: "createOrder");

        // Reaching here means ArazzoStartupValidationStartupFilter resolved the step's operationId
        // against the live OpenAPI document without throwing.
        host.ShouldNotBeNull();
    }

    [Fact]
    public async Task Startup_UnresolvableOperationId_ThrowsAggregatingTheFailure()
    {
        var exception = await Should.ThrowAsync<ArazzoStartupValidationException>(() => BuildOperationIdHostAsync(operationId: "doesNotExist"));

        exception.Errors.ShouldHaveSingleItem();
        exception.Errors[0].ShouldContain("doesNotExist");
    }

    [Fact]
    public async Task Startup_ResolvableOperationPath_Succeeds()
    {
        using var host = await BuildOperationPathHostAsync(httpMethod: "post");

        host.ShouldNotBeNull();
    }

    [Fact]
    public async Task Startup_UnresolvableOperationPath_ThrowsAggregatingTheFailure()
    {
        // Only POST /orders is mapped; DELETE has no matching operation.
        var exception = await Should.ThrowAsync<ArazzoStartupValidationException>(() => BuildOperationPathHostAsync(httpMethod: "delete"));

        exception.Errors.ShouldHaveSingleItem();
        exception.Errors[0].ShouldContain("orders");
    }

    [Fact]
    public async Task Startup_ResolvableChannelPath_Succeeds()
    {
        using var host = await BuildChannelPathHostAsync(channelName: "lightMeasured");

        host.ShouldNotBeNull();
    }

    [Fact]
    public async Task Startup_UnresolvableChannelPath_ThrowsAggregatingTheFailure()
    {
        var exception = await Should.ThrowAsync<ArazzoStartupValidationException>(() => BuildChannelPathHostAsync(channelName: "doesNotExist"));

        exception.Errors.ShouldHaveSingleItem();
        exception.Errors[0].ShouldContain("doesNotExist");
    }

    [Fact]
    public async Task Startup_MissingOpenApiProvider_ThrowsContextualError()
    {
        // AddOpenApiSource references document "v2", but only "v1" is registered via AddOpenApi.
        var exception = await Should.ThrowAsync<ArazzoStartupValidationException>(() => BuildMissingProviderHostAsync(
            registerOpenApi: true, openApiSourceDocumentName: "v2",
            registerAsyncApi: false, asyncApiSourceDocumentName: null));

        var error = exception.Errors.ShouldHaveSingleItem();
        error.ShouldContain("orders");
        error.ShouldContain("openapi", Case.Insensitive);
        error.ShouldContain("v2");
    }

    [Fact]
    public async Task Startup_MissingAsyncApiProvider_ThrowsContextualError()
    {
        var exception = await Should.ThrowAsync<ArazzoStartupValidationException>(() => BuildMissingProviderHostAsync(
            registerOpenApi: false, openApiSourceDocumentName: null,
            registerAsyncApi: true, asyncApiSourceDocumentName: "v2"));

        var error = exception.Errors.ShouldHaveSingleItem();
        error.ShouldContain("events");
        error.ShouldContain("asyncapi", Case.Insensitive);
        error.ShouldContain("v2");
    }

    [Fact]
    public async Task Startup_TwoMissingSources_AggregatesBothFailuresInOneException()
    {
        var exception = await Should.ThrowAsync<ArazzoStartupValidationException>(() => BuildMissingProviderHostAsync(
            registerOpenApi: true, openApiSourceDocumentName: "v2",
            registerAsyncApi: true, asyncApiSourceDocumentName: "v2"));

        exception.Errors.Count.ShouldBe(2);
        exception.Errors.ShouldContain(e => e.Contains("orders"));
        exception.Errors.ShouldContain(e => e.Contains("events"));
    }

    [Fact]
    public async Task Startup_ProviderNeverCompletes_ThrowsAfterConfiguredTimeout()
    {
        var neverCompletes = new TaskCompletionSource();

        var exception = await Should.ThrowAsync<ArazzoStartupValidationException>(() =>
            BuildHangingProviderHostAsync(neverCompletes.Task, TimeSpan.FromMilliseconds(50)));

        exception.Errors.ShouldHaveSingleItem().ShouldContain("did not complete within");
    }

    [Fact]
    public async Task Startup_ValidationDisabled_UnresolvedReferenceDoesNotBlockStartup()
    {
        using var host = await BuildOperationIdHostAsync(operationId: "doesNotExist", validateOnStartup: false);

        host.ShouldNotBeNull();
    }

    [Fact]
    public async Task Startup_CustomResolverRegisteredAfterAddArazzo_OverridesTheBuiltInResolver()
    {
        // ArazzoWorkspace.RegisterResolver keeps the last one registered per source type; a consumer's
        // own IArazzoSourceResolver, added via TryAddEnumerable after AddArazzo, must win over the
        // built-in OpenApiSourceResolver so replacement/decoration through DI actually works.
        var builder = Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddOpenApi("v1");
                    services.AddArazzo(options =>
                    {
                        options.WithInfo("Test Workflows", "1.0.0");
                        options.AddOpenApiSource("orders", "v1");
                        // No matching operation is mapped anywhere; the built-in resolver would fail this.
                        options.AddWorkflow("createOrder", wf => wf
                            .Step("create", s => s.OperationPath("orders", "/does-not-exist", "post")));
                    });
                    services.TryAddEnumerable(ServiceDescriptor.Singleton<IArazzoSourceResolver, AlwaysResolvesOpenApiResolver>());
                    services.AddRouting();
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapOpenApi();
                        endpoints.MapArazzo();
                    });
                });
            });

        using var host = builder.Build();
        await Should.NotThrowAsync(() => host.StartAsync());
    }

    private sealed class AlwaysResolvesOpenApiResolver : IArazzoSourceResolver
    {
        public string SourceType => ArazzoSourceDescriptionType.OpenApi;

        public bool TryResolveOperation(object document, string operationId, out JsonNode? operation)
        {
            operation = null;
            return true;
        }

        public bool TryResolveOperationPath(object document, string jsonPointer, out JsonNode? operation)
        {
            operation = null;
            return true;
        }

        public bool TryResolveChannelPath(object document, string jsonPointer, out JsonNode? channel)
        {
            channel = null;
            return true;
        }
    }

    private static async Task<IHost> BuildMissingProviderHostAsync(
        bool registerOpenApi, string? openApiSourceDocumentName,
        bool registerAsyncApi, string? asyncApiSourceDocumentName)
    {
        var builder = Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    // Always register the "v1" document so the misconfiguration is specifically the
                    // mismatched/missing document name on the Arazzo source, not an absent AsyncApi/OpenApi
                    // registration altogether.
                    services.AddOpenApi("v1");
                    services.AddAsyncApi("v1");
                    services.AddArazzo(options =>
                    {
                        options.WithInfo("Test Workflows", "1.0.0");
                        if (registerOpenApi)
                        {
                            options.AddOpenApiSource("orders", openApiSourceDocumentName!);
                            options.AddWorkflow("createOrder", wf => wf
                                .Step("create", s => s.OperationPath("orders", "/orders", "post")));
                        }

                        if (registerAsyncApi)
                        {
                            options.AddAsyncApiSource("events", asyncApiSourceDocumentName!);
                            options.AddWorkflow("publishMeasurement", wf => wf
                                .Step("publish", s => s.Channel("events", "lightMeasured", ArazzoStepAction.Send)));
                        }
                    });
                    services.AddRouting();
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapPost("/orders", () => "ok");
                        endpoints.MapOpenApi();
                        endpoints.MapAsyncApi();
                        endpoints.MapArazzo();
                    });
                });
            });

        var host = builder.Build();
        try
        {
            await host.StartAsync();
        }
        catch
        {
            host.Dispose();
            throw;
        }

        return host;
    }

    private static async Task<IHost> BuildHangingProviderHostAsync(Task neverCompletes, TimeSpan timeout)
    {
        var builder = Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddOpenApi("v1", openApiOptions => openApiOptions.AddOperationTransformer(async (_, _, ct) =>
                    {
                        await neverCompletes.WaitAsync(ct);
                    }));
                    services.AddArazzo(options =>
                    {
                        options.WithInfo("Test Workflows", "1.0.0");
                        options.StartupValidationTimeout = timeout;
                        options.AddOpenApiSource("orders", "v1");
                        options.AddWorkflow("createOrder", wf => wf
                            .Step("create", s => s.OperationPath("orders", "/orders", "post")));
                    });
                    services.AddRouting();
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapPost("/orders", () => "ok");
                        endpoints.MapOpenApi();
                        endpoints.MapArazzo();
                    });
                });
            });

        var host = builder.Build();
        try
        {
            await host.StartAsync();
        }
        catch
        {
            host.Dispose();
            throw;
        }

        return host;
    }

    private static async Task<IHost> BuildOperationIdHostAsync(string operationId, bool validateOnStartup = true)
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
                        options.ValidateSourceReferencesOnStartup = validateOnStartup;
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

    private static async Task<IHost> BuildOperationPathHostAsync(string httpMethod)
    {
        var builder = Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddOpenApi("v1");
                    services.AddArazzo(options =>
                    {
                        options.WithInfo("Test Workflows", "1.0.0");
                        options.AddOpenApiSource("orders", "v1");
                        options.AddWorkflow("createOrder", wf => wf
                            .Step("create", s => s.OperationPath("orders", "/orders", httpMethod)));
                    });
                    services.AddRouting();
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapPost("/orders", () => "ok");
                        endpoints.MapOpenApi();
                        endpoints.MapArazzo();
                    });
                });
            });

        var host = builder.Build();
        try
        {
            await host.StartAsync();
        }
        catch
        {
            host.Dispose();
            throw;
        }

        return host;
    }

    private static async Task<IHost> BuildChannelPathHostAsync(string channelName)
    {
        var builder = Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddAsyncApi("v1", asyncApiOptions => asyncApiOptions.AddDocumentTransformer((document, _, _) =>
                    {
                        document.Channels["lightMeasured"] = new ByteBard.AsyncAPI.Models.AsyncApiChannel();
                        return Task.CompletedTask;
                    }));
                    services.AddArazzo(options =>
                    {
                        options.WithInfo("Test Workflows", "1.0.0");
                        options.AddAsyncApiSource("events", "v1");
                        options.AddWorkflow("publishMeasurement", wf => wf
                            .Step("publish", s => s.Channel("events", channelName, ArazzoStepAction.Send)));
                    });
                    services.AddRouting();
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapAsyncApi();
                        endpoints.MapArazzo();
                    });
                });
            });

        var host = builder.Build();
        try
        {
            await host.StartAsync();
        }
        catch
        {
            host.Dispose();
            throw;
        }

        return host;
    }
}
