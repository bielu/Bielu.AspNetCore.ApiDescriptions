using Google.Protobuf;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Bielu.AspNetCore.AsyncApi.Scalar.Grpc;

/// <summary>
/// Endpoint mapping for the embedded gRPC-enabled Scalar bundle and the protobuf descriptor
/// endpoint the console uses to encode wire messages.
/// </summary>
public static class ScalarGrpcEndpointRouteBuilderExtensions
{
    private const string BundleResourceName = "Bielu.AspNetCore.AsyncApi.Scalar.Grpc.plugin.js";

    /// <summary>
    /// Serves the gRPC console bundle (the <c>@bielu/scalar-grpc</c> build) at
    /// <c>{path}/plugin.js</c> and the protobuf descriptors of every gRPC service mapped on this
    /// application at <c>{path}/descriptors</c> (a serialized <c>FileDescriptorSet</c>). Point
    /// <see cref="ScalarGrpcOptionsExtensions.WithGrpcClient" /> at the same <paramref name="path" />
    /// so Scalar loads this script alongside its own bundle.
    /// </summary>
    /// <remarks>
    /// The descriptor endpoint exists because AsyncAPI payload schemas are JSON Schema and carry no
    /// protobuf field numbers — the console needs real descriptors to encode gRPC-Web messages. The
    /// services are discovered from the mapped endpoints at request time, so this can be called
    /// before or after <c>MapGrpcService&lt;T&gt;()</c>.
    /// </remarks>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="path">The base path to serve the bundle from. Defaults to <see cref="ScalarGrpcDefaults.AssetsPath" />.</param>
    /// <returns>The same <see cref="IEndpointRouteBuilder" /> for chaining.</returns>
    public static IEndpointRouteBuilder MapScalarGrpcAssets(
        this IEndpointRouteBuilder endpoints,
        string path = ScalarGrpcDefaults.AssetsPath)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrEmpty(path);

        endpoints.MapScalarPluginBundle(
            path,
            typeof(ScalarGrpcEndpointRouteBuilderExtensions).Assembly,
            BundleResourceName,
            "Scalar gRPC bundle was not embedded. Build the assets npm package (npm run build).");

        var basePath = path.TrimEnd('/');
        var dataSources = endpoints.DataSources;

        endpoints.MapGet($"{basePath}/descriptors", async (HttpContext context) =>
        {
            var descriptorSet = GrpcDescriptorSetBuilder.Build(dataSources.SelectMany(source => source.Endpoints));

            context.Response.ContentType = "application/x-protobuf";
            // Revalidate on each load so newly mapped services are never masked by a stale cache.
            context.Response.Headers.CacheControl = "no-cache";
            await context.Response.Body.WriteAsync(descriptorSet.ToByteArray());
        }).ExcludeFromDescription();

        return endpoints;
    }
}
