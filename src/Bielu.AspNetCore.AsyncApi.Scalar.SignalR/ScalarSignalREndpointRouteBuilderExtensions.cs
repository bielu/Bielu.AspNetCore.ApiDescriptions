using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Bielu.AspNetCore.AsyncApi.Scalar.SignalR;

/// <summary>
/// Endpoint mapping for the embedded SignalR-enabled Scalar bundle.
/// </summary>
public static class ScalarSignalREndpointRouteBuilderExtensions
{
    private const string BundleResourceName = "Bielu.AspNetCore.AsyncApi.Scalar.SignalR.plugin.js";

    /// <summary>
    /// Serves the SignalR-enabled Scalar bundle (the <c>@bielu/scalar-signalr</c> build) at
    /// <c>{path}/bundle.js</c>. Point <see cref="ScalarSignalROptionsExtensions.WithSignalRClient" />
    /// at the same <paramref name="path" /> so Scalar loads this bundle instead of the default one.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="path">The base path to serve the bundle from. Defaults to <see cref="ScalarSignalRDefaults.AssetsPath" />.</param>
    /// <returns>The same <see cref="IEndpointRouteBuilder" /> for chaining.</returns>
    public static IEndpointRouteBuilder MapScalarSignalRAssets(
        this IEndpointRouteBuilder endpoints,
        string path = ScalarSignalRDefaults.AssetsPath)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrEmpty(path);

        var basePath = path.TrimEnd('/');
        var assembly = typeof(ScalarSignalREndpointRouteBuilderExtensions).Assembly;

        endpoints.MapGet($"{basePath}/plugin.js", async (HttpContext context) =>
        {
            await using var stream = assembly.GetManifestResourceStream(BundleResourceName);
            if (stream is null)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                await context.Response.WriteAsync(
                    "Scalar SignalR bundle was not embedded. Build the assets npm package (npm run build).");
                return;
            }

            context.Response.ContentType = "text/javascript; charset=utf-8";
            // Revalidate on each load so a redeployed bundle is never masked by a stale browser cache.
            context.Response.Headers.CacheControl = "no-cache";
            await stream.CopyToAsync(context.Response.Body);
        }).ExcludeFromDescription();

        return endpoints;
    }
}
