using Bielu.AspNetCore.AsyncApi.Extensions;
using Bielu.AspNetCore.AsyncApi.Services;
using Bielu.AspNetCore.AsyncApi.Transformers;
using ByteBard.AsyncAPI.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Tests.Integration;

/// <summary>
/// Regression tests proving registered <see cref="IAsyncApiOperationTransformer"/>s are actually invoked
/// during document generation. Previously, <see cref="AsyncApiOptions.AddOperationTransformer(IAsyncApiOperationTransformer)"/>
/// and its overloads registered transformers that were activated and disposed but never executed.
/// </summary>
public class OperationTransformerExecutionTests
{
    private const string TestDocumentName = "asyncapi-optransform";

    [Fact]
    public async Task DelegateOperationTransformer_RunsForEveryGeneratedOperation()
    {
        var callCount = 0;
        using var host = await CreateTestHostAsync(options =>
        {
            options.AddOperationTransformer((operation, context, cancellationToken) =>
            {
                context.Description.ShouldBeNull();
                callCount++;
                operation.Summary = "transformed:" + operation.Summary;
                return Task.CompletedTask;
            });
        });

        var document = await GenerateDocumentAsync(host);

        document.Operations.Count.ShouldBeGreaterThan(0);
        callCount.ShouldBe(document.Operations.Count);
        document.Operations.Values.ShouldAllBe(op => op.Summary != null && op.Summary.StartsWith("transformed:"));
    }

    [Fact]
    public async Task InstanceOperationTransformer_ModifiesEveryGeneratedOperation()
    {
        var transformer = new RecordingOperationTransformer();
        using var host = await CreateTestHostAsync(options => options.AddOperationTransformer(transformer));

        var document = await GenerateDocumentAsync(host);

        document.Operations.Count.ShouldBeGreaterThan(0);
        transformer.CallCount.ShouldBe(document.Operations.Count);
        document.Operations.Values.ShouldAllBe(op => op.Description == RecordingOperationTransformer.Marker);
    }

    [Fact]
    public async Task TypeBasedOperationTransformer_ResolvesScopedDependenciesAndRuns()
    {
        using var host = await CreateTestHostAsync(
            options => options.AddOperationTransformer<ScopedServiceOperationTransformer>(),
            services => services.AddScoped<ScopedMarkerService>());

        var document = await GenerateDocumentAsync(host);

        document.Operations.Count.ShouldBeGreaterThan(0);
        document.Operations.Values.ShouldAllBe(op => op.Description == ScopedMarkerService.Marker);
    }

    [Fact]
    public async Task OperationTransformer_ReceivesCallerSuppliedCancellationToken()
    {
        CancellationToken? observedToken = null;
        using var host = await CreateTestHostAsync(options =>
        {
            options.AddOperationTransformer((operation, context, cancellationToken) =>
            {
                observedToken ??= cancellationToken;
                return Task.CompletedTask;
            });
        });

        var documentService = host.Services.GetRequiredKeyedService<AsyncApiDocumentService>(TestDocumentName);
        using var scope = host.Services.CreateScope();
        using var cts = new CancellationTokenSource();

        await documentService.GetAsyncApiDocumentAsync(scope.ServiceProvider, httpRequest: null, cts.Token);

        observedToken.ShouldNotBeNull();
        observedToken!.Value.ShouldBe(cts.Token);
    }

    [Fact]
    public async Task OperationTransformer_CancellationStopsFurtherGeneration()
    {
        using var host = await CreateTestHostAsync(options =>
        {
            options.AddOperationTransformer((operation, context, cancellationToken) => Task.CompletedTask);
        });

        var documentService = host.Services.GetRequiredKeyedService<AsyncApiDocumentService>(TestDocumentName);
        using var scope = host.Services.CreateScope();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(() =>
            documentService.GetAsyncApiDocumentAsync(scope.ServiceProvider, httpRequest: null, cts.Token));
    }

    private sealed class RecordingOperationTransformer : IAsyncApiOperationTransformer
    {
        public const string Marker = "instance-transformer-ran";
        public int CallCount { get; private set; }

        public Task TransformAsync(AsyncApiOperation operation, AsyncApiOperationTransformerContext context,
            CancellationToken cancellationToken)
        {
            CallCount++;
            operation.Description = Marker;
            return Task.CompletedTask;
        }
    }

    private sealed class ScopedMarkerService
    {
        public const string Marker = "scoped-transformer-ran";
    }

    private sealed class ScopedServiceOperationTransformer : IAsyncApiOperationTransformer
    {
        private readonly ScopedMarkerService _service;

        public ScopedServiceOperationTransformer(ScopedMarkerService service)
        {
            _service = service;
        }

        public Task TransformAsync(AsyncApiOperation operation, AsyncApiOperationTransformerContext context,
            CancellationToken cancellationToken)
        {
            _ = _service;
            operation.Description = ScopedMarkerService.Marker;
            return Task.CompletedTask;
        }
    }

    private static async Task<AsyncApiDocument> GenerateDocumentAsync(IHost host)
    {
        var documentService = host.Services.GetRequiredKeyedService<AsyncApiDocumentService>(TestDocumentName);
        using var scope = host.Services.CreateScope();
        return await documentService.GetAsyncApiDocumentAsync(scope.ServiceProvider);
    }

    private static async Task<IHost> CreateTestHostAsync(
        Action<AsyncApiOptions> configureOptions,
        Action<IServiceCollection>? configureServices = null)
    {
        var builder = Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    configureServices?.Invoke(services);
                    services.AddAsyncApi(TestDocumentName, options =>
                    {
                        options.WithInfo("Operation Transformer Test API", "1.0.0");
                        configureOptions(options);
                    });
                    services.AddRouting();
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapAsyncApi());
                });
            });

        var host = builder.Build();
        await host.StartAsync();
        return host;
    }
}
