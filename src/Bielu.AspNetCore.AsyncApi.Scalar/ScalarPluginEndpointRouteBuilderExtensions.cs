using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Bielu.AspNetCore.AsyncApi.Scalar;

/// <summary>
/// Endpoint mapping for a Scalar console bundle embedded as a manifest resource in a protocol
/// package (the shared half of each package's <c>MapScalar*Assets</c> method).
/// </summary>
public static class ScalarPluginEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Serves an embedded console bundle at <c>{path}/plugin.js</c>. Protocol packages call this
    /// from their own <c>MapScalar*Assets</c> extension with their assembly and resource name.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="path">The base path to serve the bundle from.</param>
    /// <param name="assembly">The assembly the bundle is embedded in.</param>
    /// <param name="resourceName">The manifest resource name of the bundle.</param>
    /// <param name="missingBundleMessage">The 404 body returned when the bundle was not embedded (e.g. built without Node).</param>
    /// <returns>The same <see cref="IEndpointRouteBuilder" /> for chaining.</returns>
    public static IEndpointRouteBuilder MapScalarPluginBundle(
        this IEndpointRouteBuilder endpoints,
        string path,
        Assembly assembly,
        string resourceName,
        string missingBundleMessage)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentException.ThrowIfNullOrEmpty(resourceName);
        ArgumentException.ThrowIfNullOrEmpty(missingBundleMessage);

        var basePath = path.TrimEnd('/');

        endpoints.MapGet($"{basePath}/plugin.js", async (HttpContext context) =>
        {
            await using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                await context.Response.WriteAsync(missingBundleMessage);
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
