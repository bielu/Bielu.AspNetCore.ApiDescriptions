// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bielu.AspNetCore.AsyncApi.Attributes.Attributes;
using Bielu.AspNetCore.AsyncApi.Extensions;
using Bielu.AspNetCore.AsyncApi.Services;
using Bielu.AspNetCore.AsyncApi.Transformers;
using ByteBard.AsyncAPI.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Tests.Integration;

/// <summary>
/// Regression tests for the transformer activation/generation/disposal lifetime in
/// <see cref="AsyncApiDocumentService.GetAsyncApiDocumentAsync(IServiceProvider, Microsoft.AspNetCore.Http.HttpRequest?, CancellationToken)"/>.
///
/// Every scenario resolves the keyed <see cref="AsyncApiDocumentService"/> directly (the test project has
/// InternalsVisibleTo access) and calls it inside an explicit scope, so the activated transformer instances - and
/// their Dispose counters - can be asserted precisely, per the pattern used by
/// <see cref="Bielu.AspNetCore.AsyncApi.Tests.Unit.AuthenticationSchemeDocumentTransformerTests"/> and the test-host
/// pattern in <see cref="AsyncApiDocumentGenerationTests"/>.
///
/// Each scenario uses a static probe object (instead of a fresh instance per test) because type-based transformers
/// are activated by the library via <c>ActivatorUtilities</c>, which constructs the instance itself - the test has
/// no opportunity to pass state into the constructor except through DI. The probes are reset at the start of each
/// test; xUnit runs the [Fact] methods of a class sequentially by default, so this is safe.
/// </summary>
public class TransformerLifecycleExceptionSafetyTests
{
    private sealed class TransformerProbe
    {
        public int TransformCalls;
        public int DisposeCount;
        public bool ThrowOnTransform;

        public void Reset()
        {
            TransformCalls = 0;
            DisposeCount = 0;
        }
    }

    private sealed class ActivationProbe
    {
        public int ActivationCount;
        public bool ThrowOnActivate;

        public void Reset() => ActivationCount = 0;
    }

    private sealed class Payload
    {
        public string? Value { get; set; }
    }

    private static async Task<(IHost Host, IServiceScope Scope, AsyncApiDocumentService Service)> CreateServiceAsync(
        string documentName, Action<IServiceCollection> configureServices)
    {
        var builder = Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(configureServices);
                webBuilder.Configure(_ => { });
            });

        var host = builder.Build();
        await host.StartAsync();

        var scope = host.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredKeyedService<AsyncApiDocumentService>(documentName);
        return (host, scope, service);
    }

    // ---- Scenario 1: normal completion -------------------------------------------------------

    [AsyncApi(NormalCompletionDocument)]
    private sealed class NormalCompletionChannel
    {
        [Channel("lifecycle-normal")]
        [Message(typeof(Payload), MessageId = "payload")]
        [SubscribeOperation(OperationId = "lifecycleNormalOp")]
        public void Handle() { }
    }
    private const string NormalCompletionDocument = "lifecycle-normal-completion";

    private static readonly TransformerProbe NormalSchemaProbe = new();
    private static readonly TransformerProbe NormalOperationProbe = new();

    private sealed class NormalSchemaTransformer : IAsyncApiSchemaTransformer, IDisposable
    {
        public Task TransformAsync(AsyncApiJsonSchema schema, AsyncApiJsonSchemaTransformerContext context, CancellationToken cancellationToken)
        {
            NormalSchemaProbe.TransformCalls++;
            return Task.CompletedTask;
        }

        public void Dispose() => NormalSchemaProbe.DisposeCount++;
    }

    private sealed class NormalOperationTransformer : IAsyncApiOperationTransformer, IDisposable
    {
        public Task TransformAsync(AsyncApiOperation operation, AsyncApiOperationTransformerContext context, CancellationToken cancellationToken)
        {
            NormalOperationProbe.TransformCalls++;
            return Task.CompletedTask;
        }

        public void Dispose() => NormalOperationProbe.DisposeCount++;
    }

    [Fact]
    public async Task Normal_completion_disposes_activated_transformers_exactly_once()
    {
        NormalSchemaProbe.Reset();
        NormalOperationProbe.Reset();

        var (host, scope, service) = await CreateServiceAsync(NormalCompletionDocument, services =>
        {
            services.AddAsyncApi(NormalCompletionDocument, options =>
            {
                options.AddSchemaTransformer<NormalSchemaTransformer>();
                options.AddOperationTransformer<NormalOperationTransformer>();
            });
        });
        using var _ = host;
        using var __ = scope;

        var document = await service.GetAsyncApiDocumentAsync(scope.ServiceProvider);

        document.ShouldNotBeNull();
        // Other test fixtures in this assembly register document-agnostic ([AsyncApi] with no document
        // name) channels that are picked up by every document's generation, so more than one schema may be
        // visited; what matters here is that activation and disposal are each observed exactly once.
        NormalSchemaProbe.TransformCalls.ShouldBeGreaterThanOrEqualTo(1);
        NormalSchemaProbe.DisposeCount.ShouldBe(1);
        // Operation transformers are activated/disposed per request even though generation does not yet invoke
        // TransformAsync on them (a separate, already-known gap); the lifecycle fix still owns and releases the
        // activated instance.
        NormalOperationProbe.DisposeCount.ShouldBe(1);
    }

    // ---- Scenario 2: caller-supplied instance is never disposed by per-request cleanup -------

    [AsyncApi(CallerOwnedDocument)]
    private sealed class CallerOwnedChannel
    {
        [Channel("lifecycle-caller-owned")]
        [Message(typeof(Payload), MessageId = "payload")]
        [SubscribeOperation(OperationId = "lifecycleCallerOwnedOp")]
        public void Handle() { }
    }
    private const string CallerOwnedDocument = "lifecycle-caller-owned";

    private static readonly TransformerProbe CallerOwnedProbe = new();
    private static readonly TransformerProbe CallerOwnedPerRequestProbe = new();

    /// <summary>
    /// Registered as a pre-built instance via <c>AddSchemaTransformer(IAsyncApiSchemaTransformer)</c>. This exact
    /// object is reused across every generation request, so per-request cleanup must never dispose it - the
    /// caller owns its lifetime.
    /// </summary>
    private sealed class CallerOwnedSchemaTransformer : IAsyncApiSchemaTransformer, IDisposable
    {
        public Task TransformAsync(AsyncApiJsonSchema schema, AsyncApiJsonSchemaTransformerContext context, CancellationToken cancellationToken)
        {
            CallerOwnedProbe.TransformCalls++;
            return Task.CompletedTask;
        }

        public void Dispose() => CallerOwnedProbe.DisposeCount++;
    }

    private sealed class CallerOwnedPerRequestSchemaTransformer : IAsyncApiSchemaTransformer, IDisposable
    {
        public Task TransformAsync(AsyncApiJsonSchema schema, AsyncApiJsonSchemaTransformerContext context, CancellationToken cancellationToken)
        {
            CallerOwnedPerRequestProbe.TransformCalls++;
            return Task.CompletedTask;
        }

        public void Dispose() => CallerOwnedPerRequestProbe.DisposeCount++;
    }

    [Fact]
    public async Task Caller_supplied_instance_transformer_is_never_disposed_by_generation()
    {
        CallerOwnedProbe.Reset();
        CallerOwnedPerRequestProbe.Reset();

        var (host, scope, service) = await CreateServiceAsync(CallerOwnedDocument, services =>
        {
            services.AddAsyncApi(CallerOwnedDocument, options =>
            {
                options.AddSchemaTransformer(new CallerOwnedSchemaTransformer());
                options.AddSchemaTransformer<CallerOwnedPerRequestSchemaTransformer>();
            });
        });
        using var _ = host;
        using var __ = scope;

        await service.GetAsyncApiDocumentAsync(scope.ServiceProvider);

        var callsAfterFirstRequest = CallerOwnedProbe.TransformCalls;
        callsAfterFirstRequest.ShouldBeGreaterThanOrEqualTo(1);
        // The caller-supplied instance is reused across requests; per-request cleanup must not touch it.
        CallerOwnedProbe.DisposeCount.ShouldBe(0);
        // The type-based registration, in contrast, is activated and owned per request and must be disposed.
        CallerOwnedPerRequestProbe.DisposeCount.ShouldBe(1);

        // A second request proves the caller-owned instance is still usable - it was never disposed.
        using var secondScope = host.Services.CreateScope();
        var secondService = secondScope.ServiceProvider.GetRequiredKeyedService<AsyncApiDocumentService>(CallerOwnedDocument);
        await secondService.GetAsyncApiDocumentAsync(secondScope.ServiceProvider);
        CallerOwnedProbe.TransformCalls.ShouldBeGreaterThan(callsAfterFirstRequest);
        CallerOwnedProbe.DisposeCount.ShouldBe(0);
    }

    // ---- Scenario 3: schema failure during population disposes every activated transformer ---

    [AsyncApi(SchemaFailureDocument)]
    private sealed class SchemaFailureChannel
    {
        [Channel("lifecycle-schema-failure")]
        [Message(typeof(Payload), MessageId = "payload")]
        [SubscribeOperation(OperationId = "lifecycleSchemaFailureOp")]
        public void Handle() { }
    }
    private const string SchemaFailureDocument = "lifecycle-schema-failure";

    private static readonly TransformerProbe SchemaFailureThrowingProbe = new() { ThrowOnTransform = true };
    private static readonly TransformerProbe SchemaFailureHealthyOperationProbe = new();

    private sealed class ThrowingSchemaTransformer : IAsyncApiSchemaTransformer, IDisposable
    {
        public Task TransformAsync(AsyncApiJsonSchema schema, AsyncApiJsonSchemaTransformerContext context, CancellationToken cancellationToken)
        {
            SchemaFailureThrowingProbe.TransformCalls++;
            if (SchemaFailureThrowingProbe.ThrowOnTransform)
            {
                throw new InvalidOperationException("Probe transform failure");
            }

            return Task.CompletedTask;
        }

        public void Dispose() => SchemaFailureThrowingProbe.DisposeCount++;
    }

    private sealed class HealthyOperationTransformer : IAsyncApiOperationTransformer, IDisposable
    {
        public Task TransformAsync(AsyncApiOperation operation, AsyncApiOperationTransformerContext context, CancellationToken cancellationToken)
        {
            SchemaFailureHealthyOperationProbe.TransformCalls++;
            return Task.CompletedTask;
        }

        public void Dispose() => SchemaFailureHealthyOperationProbe.DisposeCount++;
    }

    [Fact]
    public async Task Schema_failure_during_population_disposes_all_activated_transformers_and_surfaces_original_exception()
    {
        SchemaFailureThrowingProbe.Reset();
        SchemaFailureHealthyOperationProbe.Reset();

        var (host, scope, service) = await CreateServiceAsync(SchemaFailureDocument, services =>
        {
            services.AddAsyncApi(SchemaFailureDocument, options =>
            {
                options.AddSchemaTransformer<ThrowingSchemaTransformer>();
                options.AddOperationTransformer<HealthyOperationTransformer>();
            });
        });
        using var _ = host;
        using var __ = scope;

        var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await service.GetAsyncApiDocumentAsync(scope.ServiceProvider));

        exception.Message.ShouldBe("Probe transform failure");

        // Both the failing schema transformer and the never-invoked-but-activated operation transformer must be
        // disposed exactly once, even though generation failed before reaching the post-processing stage - this
        // is the exact defect the diagnostic probe in REPOSITORY-IMPROVEMENTS.md observed (dispose count stayed
        // at zero after a schema-transformer failure).
        SchemaFailureThrowingProbe.DisposeCount.ShouldBe(1);
        SchemaFailureHealthyOperationProbe.DisposeCount.ShouldBe(1);
    }

    // ---- Scenario 4: cancellation mid-generation still disposes activated transformers -------

    [AsyncApi(CancellationDocument)]
    private sealed class CancellationChannel
    {
        [Channel("lifecycle-cancellation")]
        [Message(typeof(Payload), MessageId = "payload")]
        [SubscribeOperation(OperationId = "lifecycleCancellationOp")]
        public void Handle() { }
    }
    private const string CancellationDocument = "lifecycle-cancellation";

    private static readonly TransformerProbe CancellationProbe = new();

    private sealed class CancellationAwareSchemaTransformer : IAsyncApiSchemaTransformer, IDisposable
    {
        public Task TransformAsync(AsyncApiJsonSchema schema, AsyncApiJsonSchemaTransformerContext context, CancellationToken cancellationToken)
        {
            CancellationProbe.TransformCalls++;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public void Dispose() => CancellationProbe.DisposeCount++;
    }

    [Fact]
    public async Task Cancellation_mid_generation_still_disposes_activated_transformers()
    {
        CancellationProbe.Reset();

        var (host, scope, service) = await CreateServiceAsync(CancellationDocument, services =>
        {
            services.AddAsyncApi(CancellationDocument, options =>
            {
                options.AddSchemaTransformer<CancellationAwareSchemaTransformer>();
            });
        });
        using var _ = host;
        using var __ = scope;

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await service.GetAsyncApiDocumentAsync(scope.ServiceProvider, cancellationToken: cts.Token));

        CancellationProbe.DisposeCount.ShouldBe(1);
    }

    // ---- Scenario 5: partial activation failure disposes only what activated successfully ----

    [AsyncApi(PartialActivationDocument)]
    private sealed class PartialActivationChannel
    {
        [Channel("lifecycle-partial-activation")]
        [Message(typeof(Payload), MessageId = "payload")]
        [SubscribeOperation(OperationId = "lifecyclePartialActivationOp")]
        public void Handle() { }
    }
    private const string PartialActivationDocument = "lifecycle-partial-activation";

    private static readonly TransformerProbe PartialActivationHealthyProbe = new();
    private static readonly ActivationProbe PartialActivationThrowingActivationProbe = new() { ThrowOnActivate = true };

    private sealed class HealthySchemaTransformer : IAsyncApiSchemaTransformer, IDisposable
    {
        public Task TransformAsync(AsyncApiJsonSchema schema, AsyncApiJsonSchemaTransformerContext context, CancellationToken cancellationToken)
        {
            PartialActivationHealthyProbe.TransformCalls++;
            return Task.CompletedTask;
        }

        public void Dispose() => PartialActivationHealthyProbe.DisposeCount++;
    }

    /// <summary>Throws during activation itself (in its constructor), before it is ever used to transform anything.</summary>
    private sealed class ThrowsOnActivateSchemaTransformer : IAsyncApiSchemaTransformer, IDisposable
    {
        public ThrowsOnActivateSchemaTransformer()
        {
            PartialActivationThrowingActivationProbe.ActivationCount++;
            throw new InvalidOperationException("Probe activation failure");
        }

        public Task TransformAsync(AsyncApiJsonSchema schema, AsyncApiJsonSchemaTransformerContext context, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public void Dispose()
        {
        }
    }

    [Fact]
    public async Task Activation_failure_still_disposes_the_instances_activated_before_it()
    {
        PartialActivationHealthyProbe.Reset();
        PartialActivationThrowingActivationProbe.Reset();

        var (host, scope, service) = await CreateServiceAsync(PartialActivationDocument, services =>
        {
            services.AddAsyncApi(PartialActivationDocument, options =>
            {
                // First transformer activates successfully...
                options.AddSchemaTransformer<HealthySchemaTransformer>();
                // ...the second throws during activation (its constructor runs eagerly, before any TransformAsync
                // call for either transformer).
                options.AddSchemaTransformer<ThrowsOnActivateSchemaTransformer>();
            });
        });
        using var _ = host;
        using var __ = scope;

        var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await service.GetAsyncApiDocumentAsync(scope.ServiceProvider));

        exception.Message.ShouldBe("Probe activation failure");

        // The transformer activated before the failing one must still be disposed, even though it was never used
        // (population never got a chance to run) - ownership was established the moment activation succeeded.
        PartialActivationHealthyProbe.DisposeCount.ShouldBe(1);
        PartialActivationThrowingActivationProbe.ActivationCount.ShouldBe(1);
    }

    // ---- Scenario 6: a disposal failure does not prevent other instances from being disposed -

    [AsyncApi(DisposalFailureDocument)]
    private sealed class DisposalFailureChannel
    {
        [Channel("lifecycle-disposal-failure")]
        [Message(typeof(Payload), MessageId = "payload")]
        [SubscribeOperation(OperationId = "lifecycleDisposalFailureOp")]
        public void Handle() { }
    }
    private const string DisposalFailureDocument = "lifecycle-disposal-failure";

    private static readonly TransformerProbe DisposalFailureFirstProbe = new();
    private static readonly TransformerProbe DisposalFailureSecondProbe = new();

    private sealed class FirstDisposalFailureSchemaTransformer : IAsyncApiSchemaTransformer, IDisposable
    {
        public Task TransformAsync(AsyncApiJsonSchema schema, AsyncApiJsonSchemaTransformerContext context, CancellationToken cancellationToken)
        {
            DisposalFailureFirstProbe.TransformCalls++;
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            DisposalFailureFirstProbe.DisposeCount++;
            throw new InvalidOperationException("Probe dispose failure (first)");
        }
    }

    private sealed class SecondDisposalFailureSchemaTransformer : IAsyncApiSchemaTransformer, IDisposable
    {
        public Task TransformAsync(AsyncApiJsonSchema schema, AsyncApiJsonSchemaTransformerContext context, CancellationToken cancellationToken)
        {
            DisposalFailureSecondProbe.TransformCalls++;
            return Task.CompletedTask;
        }

        public void Dispose() => DisposalFailureSecondProbe.DisposeCount++;
    }

    [Fact]
    public async Task Disposal_failure_in_one_transformer_does_not_prevent_others_from_being_disposed()
    {
        DisposalFailureFirstProbe.Reset();
        DisposalFailureSecondProbe.Reset();

        var (host, scope, service) = await CreateServiceAsync(DisposalFailureDocument, services =>
        {
            services.AddAsyncApi(DisposalFailureDocument, options =>
            {
                options.AddSchemaTransformer<FirstDisposalFailureSchemaTransformer>();
                options.AddSchemaTransformer<SecondDisposalFailureSchemaTransformer>();
            });
        });
        using var _ = host;
        using var __ = scope;

        // Generation itself succeeds; only disposal fails. Because there is no original generation exception to
        // protect, the disposal failure is the exception surfaced to the caller.
        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await service.GetAsyncApiDocumentAsync(scope.ServiceProvider));

        DisposalFailureFirstProbe.DisposeCount.ShouldBe(1);
        // Even though the first transformer's Dispose threw, the second must still have been disposed.
        DisposalFailureSecondProbe.DisposeCount.ShouldBe(1);
    }

    // ---- Scenario 7: a generation failure AND a disposal failure are both surfaced -----------

    [AsyncApi(CombinedFailureDocument)]
    private sealed class CombinedFailureChannel
    {
        [Channel("lifecycle-combined-failure")]
        [Message(typeof(Payload), MessageId = "payload")]
        [SubscribeOperation(OperationId = "lifecycleCombinedFailureOp")]
        public void Handle() { }
    }
    private const string CombinedFailureDocument = "lifecycle-combined-failure";

    private static readonly TransformerProbe CombinedDisposalFailureProbe = new();
    private static readonly TransformerProbe CombinedGenerationFailureProbe = new();

    private sealed class DisposalFailureButHealthyTransformTransformer : IAsyncApiSchemaTransformer, IDisposable
    {
        public Task TransformAsync(AsyncApiJsonSchema schema, AsyncApiJsonSchemaTransformerContext context, CancellationToken cancellationToken)
        {
            CombinedDisposalFailureProbe.TransformCalls++;
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            CombinedDisposalFailureProbe.DisposeCount++;
            throw new InvalidOperationException("Probe dispose failure (combined)");
        }
    }

    private sealed class GenerationFailureTransformer : IAsyncApiSchemaTransformer
    {
        public Task TransformAsync(AsyncApiJsonSchema schema, AsyncApiJsonSchemaTransformerContext context, CancellationToken cancellationToken)
        {
            CombinedGenerationFailureProbe.TransformCalls++;
            throw new InvalidOperationException("Probe generation failure");
        }
    }

    [Fact]
    public async Task Generation_and_disposal_failures_are_both_surfaced_without_hiding_the_original_error()
    {
        CombinedDisposalFailureProbe.Reset();
        CombinedGenerationFailureProbe.Reset();

        var (host, scope, service) = await CreateServiceAsync(CombinedFailureDocument, services =>
        {
            services.AddAsyncApi(CombinedFailureDocument, options =>
            {
                // Registered first so it is activated (and therefore owned/disposed) before the transformer whose
                // TransformAsync throws during population.
                options.AddSchemaTransformer<DisposalFailureButHealthyTransformTransformer>();
                options.AddSchemaTransformer<GenerationFailureTransformer>();
            });
        });
        using var _ = host;
        using var __ = scope;

        var thrown = await Record.ExceptionAsync(async () =>
            await service.GetAsyncApiDocumentAsync(scope.ServiceProvider));

        thrown.ShouldNotBeNull();

        // Disposal must have been attempted for the activated transformer regardless of the generation failure.
        CombinedDisposalFailureProbe.DisposeCount.ShouldBe(1);

        // The original generation failure must remain observable - either directly, or as the first inner
        // exception of an AggregateException that also carries the disposal failure. It must never be silently
        // replaced or hidden by the disposal failure.
        if (thrown is AggregateException aggregate)
        {
            aggregate.InnerExceptions.Count.ShouldBe(2);
            aggregate.InnerExceptions[0].Message.ShouldBe("Probe generation failure");
            aggregate.InnerExceptions.ShouldContain(e => e.Message == "Probe dispose failure (combined)");
        }
        else
        {
            thrown.Message.ShouldBe("Probe generation failure");
        }
    }
}
