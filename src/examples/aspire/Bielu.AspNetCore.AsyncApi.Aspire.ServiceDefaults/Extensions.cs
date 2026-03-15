// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
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
        // Ensure librdkafka native library is pre-loaded with a compatible variant
        // before any Confluent.Kafka type is used (including Aspire health checks).
        EnsureLibrdkafka();

        builder.ConfigureOpenTelemetry();
        builder.AddDefaultHealthChecks();
        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
        });

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
                    .AddMeter(MessagingMetrics.MeterName);
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
    /// Registers the shared Kafka event publisher and messaging metrics.
    /// Call this in services that publish events via Kafka.
    /// Requires a Kafka producer to be registered via <c>AddKafkaProducer</c>.
    /// </summary>
    public static IHostApplicationBuilder AddMessaging(this IHostApplicationBuilder builder)
    {
        builder.Services.AddSingleton<MessagingMetrics>();
        builder.Services.AddSingleton<IEventPublisher, KafkaEventPublisher>();

        return builder;
    }

    /// <summary>
    /// Registers the shared Redis/Valkey cache service.
    /// Call this only in services that use Redis/Valkey for caching.
    /// Requires an <c>IConnectionMultiplexer</c> to be registered via <c>AddRedisClient</c>.
    /// </summary>
    public static IHostApplicationBuilder AddCaching(this IHostApplicationBuilder builder)
    {
        builder.Services.AddSingleton<ICacheService, RedisCacheService>();

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

    /// <summary>
    /// Pre-loads the correct librdkafka native library variant for the current platform
    /// and registers a DLL import resolver so that Confluent.Kafka's P/Invoke calls
    /// resolve to the pre-loaded library.
    /// On Linux the centos8 variant is preferred because it statically links SASL
    /// (avoiding the missing libsasl2.so.3 issue on Ubuntu 24.04+).
    /// On Windows/macOS the standard library is loaded from the runtimes directory.
    /// NOTE: Uses ProcessArchitecture (not OSArchitecture) because the native DLL must
    /// match the running process — e.g., x64 .NET on Windows ARM64 needs win-x64 binaries.
    /// librdkafka.redist 2.12.0 ships: linux-arm64, linux-x64, osx-arm64, osx-x64,
    /// win-x64, win-x86. Notably win-arm64 is NOT shipped — Windows ARM64 users must
    /// use the x64 .NET SDK (runs under emulation) until upstream adds win-arm64 support.
    /// See: https://github.com/confluentinc/confluent-kafka-dotnet/issues/778
    /// </summary>
    private static void EnsureLibrdkafka()
    {
        IntPtr handle = IntPtr.Zero;
        string baseDir = AppContext.BaseDirectory;

        foreach (var candidate in GetLibrdkafkaCandidates(baseDir))
        {
            if (File.Exists(candidate) && NativeLibrary.TryLoad(candidate, out handle))
            {
                break;
            }
        }

        if (handle == IntPtr.Zero)
        {
            return;
        }

        try
        {
            NativeLibrary.SetDllImportResolver(
                typeof(Confluent.Kafka.ProducerBuilder<string, string>).Assembly,
                (libraryName, _, _) => libraryName == "librdkafka" ? handle : IntPtr.Zero);
        }
        catch (InvalidOperationException)
        {
            // A resolver is already registered for this assembly (e.g., called from AppHost) — safe to ignore
        }
    }

    /// <summary>
    /// Returns candidate native library paths in priority order for the current platform.
    /// </summary>
    private static IEnumerable<string> GetLibrdkafkaCandidates(string baseDir)
    {
        var arch = RuntimeInformation.ProcessArchitecture;

        if (OperatingSystem.IsLinux())
        {
            var rid = arch == Architecture.Arm64 ? "linux-arm64" : "linux-x64";
            // centos8 variant has SASL statically linked (no libsasl2.so.3 dependency)
            yield return Path.Combine(baseDir, "runtimes", rid, "native", "centos8-librdkafka.so");
            yield return Path.Combine(baseDir, "runtimes", rid, "native", "librdkafka.so");
        }
        else if (OperatingSystem.IsWindows())
        {
            var rid = arch switch
            {
                Architecture.X86 => "win-x86",
                Architecture.Arm64 => "win-arm64",
                _ => "win-x64",
            };
            yield return Path.Combine(baseDir, "runtimes", rid, "native", "librdkafka.dll");
            // Fallback: on ARM64 try x64 (works when process runs under x64 emulation)
            if (rid != "win-x64")
                yield return Path.Combine(baseDir, "runtimes", "win-x64", "native", "librdkafka.dll");
        }
        else if (OperatingSystem.IsMacOS())
        {
            var rid = arch == Architecture.Arm64 ? "osx-arm64" : "osx-x64";
            yield return Path.Combine(baseDir, "runtimes", rid, "native", "librdkafka.dylib");
        }
    }
}
