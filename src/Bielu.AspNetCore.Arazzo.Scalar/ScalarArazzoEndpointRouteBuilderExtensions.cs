using Bielu.AspNetCore.AsyncApi.Scalar;
using Microsoft.AspNetCore.Routing;

namespace Bielu.AspNetCore.Arazzo.Scalar;

/// <summary>
/// Endpoint mapping for the embedded Scalar Arazzo bundle.
/// </summary>
public static class ScalarArazzoEndpointRouteBuilderExtensions
{
    private const string BundleResourceName = "Bielu.AspNetCore.Arazzo.Scalar.plugin.js";

    /// <summary>
    /// Serves the Arazzo plugin bundle (the <c>@bielu/scalar-arazzo</c> build) at
    /// <c>{path}/plugin.js</c>. Point <see cref="ScalarArazzoOptionsExtensions.WithArazzo" /> at the
    /// same <paramref name="path" /> so Scalar loads this script alongside its own bundle.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="path">The base path to serve the bundle from. Defaults to <see cref="ScalarArazzoDefaults.AssetsPath" />.</param>
    /// <returns>The same <see cref="IEndpointRouteBuilder" /> for chaining.</returns>
    public static IEndpointRouteBuilder MapScalarArazzoAssets(
        this IEndpointRouteBuilder endpoints,
        string path = ScalarArazzoDefaults.AssetsPath)
    {
        return endpoints.MapScalarPluginBundle(
            path,
            typeof(ScalarArazzoEndpointRouteBuilderExtensions).Assembly,
            BundleResourceName,
            "Scalar Arazzo bundle was not embedded. Build the assets npm package (npm run build).");
    }
}
