// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bielu.AspNetCore.AsyncApi.Aspire.ServiceDefaults.Caching;
using Bielu.AspNetCore.AsyncApi.Aspire.ServiceDefaults.Diagnostics;
using Bielu.AspNetCore.AsyncApi.Aspire.ServiceDefaults.Messaging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Shared service defaults for the Aspire-based microservices.
/// </summary>
public static class Extensions
{
    /// <summary>
    /// Adds common .NET Aspire services: service discovery, resilience, health checks, and OpenTelemetry.
    /// </summary>
    public static IHostApplicationBuilder AddServiceDefaults(this IHostApplicationBuilder builder)
    {
        builder.ConfigureOpenTelemetry();
        builder.AddDefaultHealthChecks();
        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
        });

        // Register the shared Kafka event publisher and messaging metrics
        builder.Services.AddSingleton<MessagingMetrics>();
        builder.Services.AddSingleton<IEventPublisher, KafkaEventPublisher>();

        // Register the shared Redis/Valkey cache service
        builder.Services.AddSingleton<ICacheService, RedisCacheService>();

        return builder;
    }

    /// <summary>
    /// Configures OpenTelemetry with OTLP exporter.
    /// The shared <see cref="DiagnosticsNames.Messaging"/> source is always registered.
    /// </summary>
    public static IHostApplicationBuilder ConfigureOpenTelemetry(this IHostApplicationBuilder builder)
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter(Messaging.MessagingMetrics.MeterName);
            })
            .WithTracing(tracing =>
            {
                tracing.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddSource(DiagnosticsNames.Messaging);
            });

        // Register the Messaging ActivitySource as a singleton via DI
        builder.Services.AddKeyedSingleton(
            DiagnosticsNames.Messaging,
            new ActivitySourceProvider(DiagnosticsNames.Messaging));

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    /// <summary>
    /// Registers a custom OpenTelemetry meter for this service's metrics.
    /// </summary>
    public static IHostApplicationBuilder AddServiceMetrics(this IHostApplicationBuilder builder, string meterName)
    {
        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics => metrics.AddMeter(meterName));

        return builder;
    }

    /// <summary>
    /// Registers a singleton <see cref="ActivitySourceProvider"/> for the given source name
    /// and subscribes it to OpenTelemetry tracing.
    /// </summary>
    public static IHostApplicationBuilder AddServiceTracing(this IHostApplicationBuilder builder, string sourceName)
    {
        builder.Services.AddSingleton(new ActivitySourceProvider(sourceName));
        builder.Services.AddOpenTelemetry()
            .WithTracing(tracing => tracing.AddSource(sourceName));

        return builder;
    }

    private static IHostApplicationBuilder AddOpenTelemetryExporters(this IHostApplicationBuilder builder)
    {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        return builder;
    }

    /// <summary>
    /// Adds default health check endpoints.
    /// </summary>
    public static IHostApplicationBuilder AddDefaultHealthChecks(this IHostApplicationBuilder builder)
    {
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    /// <summary>
    /// Maps default health check endpoints for readiness and liveness probes.
    /// </summary>
    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/health");
        app.MapHealthChecks("/alive", new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("live")
        });

        return app;
    }
}
