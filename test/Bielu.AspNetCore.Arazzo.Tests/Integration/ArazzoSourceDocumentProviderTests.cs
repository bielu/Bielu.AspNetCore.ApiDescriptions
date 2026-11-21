using Bielu.Arazzo.Models;
using Bielu.AspNetCore.Arazzo.Extensions;
using Bielu.AspNetCore.Arazzo.Services;
using Bielu.AspNetCore.Arazzo.SourceResolvers;
using Bielu.AspNetCore.Arazzo.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.Arazzo.Tests.Integration;

/// <summary>Startup validation of sources self-wired through an <see cref="IArazzoSourceDocumentProvider"/>.</summary>
public class ArazzoSourceDocumentProviderTests
{
    [Fact]
    public async Task Startup_CustomProviderDocument_ResolvesStepsAgainstIt()
    {
        // Arrange
        var configure = WithPaymentsSource(s => s.OperationPath("payments", "/payments", "post"));

        // Act
        using var host = await BuildHostAsync(configure, registerProvider: true);

        // Assert
        host.ShouldNotBeNull();
    }

    [Fact]
    public async Task Startup_CustomProviderDocument_UnresolvableStepIsReported()
    {
        // Arrange
        var configure = WithPaymentsSource(s => s.OperationPath("payments", "/refunds", "post"));

        // Act
        var exception = await Should.ThrowAsync<ArazzoStartupValidationException>(() =>
            BuildHostAsync(configure, registerProvider: true));

        // Assert
        exception.Errors.ShouldHaveSingleItem().ShouldContain("did not resolve");
    }

    [Fact]
    public async Task Startup_CustomProviderReturnsNull_BuiltInProviderStillSuppliesAppDocuments()
    {
        // Arrange: the custom provider goes first for "openapi" but only owns "payments"; "v1" falls through.
        var configure = WithPaymentsSource(s => s.OperationPath("orders", "/orders", "post"));

        // Act
        using var host = await BuildHostAsync(configure, registerProvider: true);

        // Assert
        host.ShouldNotBeNull();
    }

    [Fact]
    public async Task Startup_NoProviderHasTheDocument_ThrowsContextualError()
    {
        // Arrange: without the custom provider, the built-in one has no "payments" document.
        var configure = WithPaymentsSource(s => s.OperationPath("payments", "/payments", "post"));

        // Act
        var exception = await Should.ThrowAsync<ArazzoStartupValidationException>(() =>
            BuildHostAsync(configure, registerProvider: false));

        // Assert
        var error = exception.Errors.ShouldHaveSingleItem();
        error.ShouldContain("'payments' (openapi)");
        error.ShouldContain("AddOpenApi(\"partner\"");
    }

    [Fact]
    public async Task Startup_NoProviderForSourceType_ThrowsContextualError()
    {
        // Arrange
        Action<ArazzoOptions> configure = options => options
            .AddSelfWiredSource("flows", ArazzoSourceDescriptionType.Arazzo, "other", "/arazzo/other.json")
            .AddWorkflow("pay", wf => wf.Step("create", s => s.OperationPath("orders", "/orders", "post")));

        // Act
        var exception = await Should.ThrowAsync<ArazzoStartupValidationException>(() =>
            BuildHostAsync(configure, registerProvider: false));

        // Assert
        var error = exception.Errors.ShouldHaveSingleItem();
        error.ShouldContain("'flows'");
        error.ShouldContain($"no {nameof(IArazzoSourceDocumentProvider)} is registered for that type");
    }

    [Fact]
    public async Task Startup_NoProviderForSourceType_StepsOnThatSourceAreNotReportedAgain()
    {
        // Arrange
        Action<ArazzoOptions> configure = options => options
            .AddSelfWiredSource("flows", ArazzoSourceDescriptionType.Arazzo, "other", "/arazzo/other.json")
            .AddWorkflow("pay", wf => wf.Step("create", s => s.OperationPath("flows", "/workflows/0", "post")));

        // Act
        var exception = await Should.ThrowAsync<ArazzoStartupValidationException>(() =>
            BuildHostAsync(configure, registerProvider: false));

        // Assert
        exception.Errors.ShouldHaveSingleItem().ShouldContain("no IArazzoSourceDocumentProvider");
    }

    private static Action<ArazzoOptions> WithPaymentsSource(Action<ArazzoStepBuilder> configureStep) => options => options
        .AddSelfWiredSource("payments", ArazzoSourceDescriptionType.OpenApi, "partner", "https://payments.example.com/openapi.json")
        .AddWorkflow("pay", wf => wf.Step("step", configureStep));

    private static async Task<IHost> BuildHostAsync(Action<ArazzoOptions> configure, bool registerProvider)
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
                        configure(options);
                    });
                    if (registerProvider)
                    {
                        services.TryAddEnumerable(ServiceDescriptor.Singleton<IArazzoSourceDocumentProvider, PartnerOpenApiProvider>());
                    }

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

    /// <summary>Supplies a partner's OpenAPI document, with only <c>POST /payments</c>, under the name "partner".</summary>
    private sealed class PartnerOpenApiProvider : IArazzoSourceDocumentProvider
    {
        public string SourceType => ArazzoSourceDescriptionType.OpenApi.Value;

        public ValueTask<object?> GetDocumentAsync(string documentName, CancellationToken cancellationToken)
        {
            if (documentName != "partner")
            {
                return ValueTask.FromResult<object?>(null);
            }

            var document = new OpenApiDocument
            {
                Paths = new OpenApiPaths
                {
                    ["/payments"] = new OpenApiPathItem
                    {
                        Operations = new Dictionary<HttpMethod, OpenApiOperation> { [HttpMethod.Post] = new() },
                    },
                },
            };
            return ValueTask.FromResult<object?>(document);
        }
    }
}
