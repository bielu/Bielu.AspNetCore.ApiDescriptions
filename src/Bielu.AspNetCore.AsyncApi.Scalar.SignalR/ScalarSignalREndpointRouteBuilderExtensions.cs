using Microsoft.AspNetCore.Routing;

namespace Bielu.AspNetCore.AsyncApi.Scalar.SignalR;

/// <summary>
/// Endpoint mapping for the embedded SignalR-enabled Scalar bundle.
/// </summary>
public static class ScalarSignalREndpointRouteBuilderExtensions
{
    private const string BundleResourceName = "Bielu.AspNetCore.AsyncApi.Scalar.SignalR.plugin.js";

    /// <summary>
    /// Serves the SignalR console bundle (the <c>@bielu/scalar-signalr</c> build) at
    /// <c>{path}/plugin.js</c>. Point <see cref="ScalarSignalROptionsExtensions.WithSignalRClient" />
    /// at the same <paramref name="path" /> so Scalar loads this script alongside its own bundle.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="path">The base path to serve the bundle from. Defaults to <see cref="ScalarSignalRDefaults.AssetsPath" />.</param>
    /// <returns>The same <see cref="IEndpointRouteBuilder" /> for chaining.</returns>
    public static IEndpointRouteBuilder MapScalarSignalRAssets(
        this IEndpointRouteBuilder endpoints,
        string path = ScalarSignalRDefaults.AssetsPath)
    {
        return endpoints.MapScalarPluginBundle(
            path,
            typeof(ScalarSignalREndpointRouteBuilderExtensions).Assembly,
            BundleResourceName,
            "Scalar SignalR bundle was not embedded. Build the assets npm package (npm run build).");
    }
}
