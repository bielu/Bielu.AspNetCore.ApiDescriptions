using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Bielu.AspNetCore.AsyncApi.Scalar.Broker;

/// <summary>
/// Endpoint mapping for the embedded broker-enabled Scalar bundle and the proxy endpoints the
/// console publishes and tails through.
/// </summary>
public static class ScalarBrokerEndpointRouteBuilderExtensions
{
    private const string BundleResourceName = "Bielu.AspNetCore.AsyncApi.Scalar.Broker.plugin.js";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Serves the broker console bundle (the <c>@bielu/scalar-broker</c> build) at
    /// <c>{path}/plugin.js</c>, plus the three proxy endpoints the console drives the broker
    /// through: <c>GET {path}/connections</c>, <c>POST {path}/publish</c> and
    /// <c>GET {path}/tail</c>. Point <see cref="ScalarBrokerOptionsExtensions.WithBrokerClient" /> at
    /// the same <paramref name="path" /> so Scalar loads the script alongside its own bundle.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The proxy endpoints exist because a browser cannot speak Kafka, MQTT or AMQP. They forward to
    /// the <see cref="IBrokerBridge" /> registered for each connection by
    /// <c>AddScalarBrokerBridge</c>, which must be called first.
    /// </para>
    /// <para>
    /// <strong>These endpoints can publish to your broker.</strong> Secure them — the returned
    /// builder covers the three proxy endpoints, so
    /// <c>app.MapScalarBrokerAssets().RequireAuthorization("Admin")</c> is the intended usage.
    /// Outside the Development environment they refuse to serve callers unless authorization
    /// metadata is present or <see cref="ScalarBrokerBridgeOptions.AllowAnonymous" /> is set. The
    /// bundle itself is not covered: it is static JavaScript carrying no configuration or secrets,
    /// and the console is useless without the proxy behind it.
    /// </para>
    /// </remarks>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="path">The base path to serve from. Defaults to <see cref="ScalarBrokerDefaults.AssetsPath" />.</param>
    /// <returns>A convention builder covering the three proxy endpoints.</returns>
    public static IEndpointConventionBuilder MapScalarBrokerAssets(
        this IEndpointRouteBuilder endpoints,
        string path = ScalarBrokerDefaults.AssetsPath)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrEmpty(path);

        endpoints.MapScalarPluginBundle(
            path,
            typeof(ScalarBrokerEndpointRouteBuilderExtensions).Assembly,
            BundleResourceName,
            "Scalar broker bundle was not embedded. Build the assets npm package (npm run build).");

        var basePath = path.TrimEnd('/');

        var connections = endpoints.MapGet($"{basePath}/connections", (HttpContext context) =>
        {
            if (!Allowed(context))
            {
                return Forbidden();
            }

            var registry = context.RequestServices.GetRequiredService<IBrokerBridgeRegistry>();
            return Results.Json(registry.Connections, JsonOptions);
        }).ExcludeFromDescription();

        var publish = endpoints.MapPost($"{basePath}/publish", async (HttpContext context) =>
        {
            if (!Allowed(context))
            {
                return Forbidden();
            }

            PublishBody? body;
            try
            {
                body = await context.Request.ReadFromJsonAsync<PublishBody>(JsonOptions, context.RequestAborted);
            }
            catch (JsonException exception)
            {
                return Results.Problem($"The request body is not valid JSON: {exception.Message}", statusCode: StatusCodes.Status400BadRequest);
            }

            if (body is null || string.IsNullOrEmpty(body.Connection) || string.IsNullOrEmpty(body.Channel) || body.Payload is null)
            {
                return Results.Problem("'connection', 'channel' and 'payload' are required.", statusCode: StatusCodes.Status400BadRequest);
            }

            if (!TryResolve(context, body.Connection, out var bridge, out var problem))
            {
                return problem;
            }

            var receipt = await bridge.PublishAsync(
                new BrokerPublishRequest(body.Channel, body.Payload, body.Key, body.Headers),
                context.RequestAborted);

            return Results.Json(receipt, JsonOptions);
        }).ExcludeFromDescription();

        var tail = endpoints.MapGet($"{basePath}/tail", async (HttpContext context, string? connection, string? channel) =>
        {
            if (!Allowed(context))
            {
                return Forbidden();
            }

            if (string.IsNullOrEmpty(connection) || string.IsNullOrEmpty(channel))
            {
                return Results.Problem("'connection' and 'channel' query parameters are required.", statusCode: StatusCodes.Status400BadRequest);
            }

            if (!TryResolve(context, connection, out var bridge, out var problem))
            {
                return problem;
            }

            // Commit the SSE headers before the first message: the console renders a "connected"
            // state off the response headers, and a broker with no traffic would otherwise leave it
            // waiting on a response that never starts.
            context.Response.ContentType = "text/event-stream";
            context.Response.Headers.CacheControl = "no-cache";
            // Proxies that buffer would defeat a tail stream entirely.
            context.Response.Headers["X-Accel-Buffering"] = "no";
            await context.Response.Body.FlushAsync(context.RequestAborted);

            try
            {
                await foreach (var message in bridge.TailAsync(new BrokerTailRequest(channel), context.RequestAborted))
                {
                    await context.Response.WriteAsync($"data: {JsonSerializer.Serialize(message, JsonOptions)}\n\n", context.RequestAborted);
                    await context.Response.Body.FlushAsync(context.RequestAborted);
                }
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                // The console closed the stream; that is how a tail ends, not a fault.
            }

            return Results.Empty;
        }).ExcludeFromDescription();

        return new CompositeConventionBuilder([connections, publish, tail]);
    }

    private static bool Allowed(HttpContext context) =>
        context.RequestServices.GetRequiredService<BrokerBridgeAccessGuard>().IsAllowed(context);

    private static IResult Forbidden() =>
        Results.Problem(
            "The Scalar broker console proxy is not authorized in this environment. See the application log for how to enable it.",
            statusCode: StatusCodes.Status403Forbidden);

    private static bool TryResolve(
        HttpContext context,
        string connectionName,
        out IBrokerBridge bridge,
        out IResult problem)
    {
        var registry = context.RequestServices.GetRequiredService<IBrokerBridgeRegistry>();
        if (registry.TryGetBridge(connectionName, out var resolved))
        {
            bridge = resolved;
            problem = Results.Empty;
            return true;
        }

        bridge = null!;
        var known = registry.Connections.Count == 0
            ? "no connections are registered"
            : $"known connections: {string.Join(", ", registry.Connections.Select(static c => c.Name))}";
        problem = Results.Problem(
            $"No broker connection named '{connectionName}' is registered ({known}).",
            statusCode: StatusCodes.Status404NotFound);
        return false;
    }

    private sealed record PublishBody(
        string? Connection,
        string? Channel,
        string? Payload,
        string? Key,
        Dictionary<string, string>? Headers);

    /// <summary>
    /// Applies conventions to every proxy endpoint at once, so one
    /// <c>RequireAuthorization</c> on the result of <c>MapScalarBrokerAssets</c> covers all of them.
    /// </summary>
    private sealed class CompositeConventionBuilder(IReadOnlyList<IEndpointConventionBuilder> builders)
        : IEndpointConventionBuilder
    {
        public void Add(Action<EndpointBuilder> convention)
        {
            foreach (var builder in builders)
            {
                builder.Add(convention);
            }
        }

        public void Finally(Action<EndpointBuilder> finallyConvention)
        {
            foreach (var builder in builders)
            {
                builder.Finally(finallyConvention);
            }
        }
    }
}
