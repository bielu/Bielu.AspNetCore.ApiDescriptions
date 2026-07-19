using System.Text;
using System.Text.Json;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Scalar.Aspire;

namespace Bielu.AspNetCore.AsyncApi.Scalar.Grpc.Aspire;

/// <summary>
/// Extension methods that add the interactive gRPC console to a Scalar.Aspire resource.
/// </summary>
public static class ScalarGrpcAspireExtensions
{
    /// <summary>
    /// The default CDN URL for the published <c>@bielu/scalar-grpc-standalone</c> full bundle. Pinned to the
    /// npm version this package was built against (an immutable jsDelivr asset; the constant is
    /// generated from the npm package.json at build time) so deployed packages always load the bundle
    /// released with them rather than floating to whatever is latest on npm. This must be the
    /// standalone bundle (Scalar + the gRPC console in one script), not <c>dist/plugin.js</c>:
    /// it replaces <see cref="ScalarAspireOptions.BundleUrl" />, so nothing else loads Scalar itself.
    /// </summary>
    public const string DefaultBundleUrl =
        "https://cdn.jsdelivr.net/npm/@bielu/scalar-grpc-standalone@" + ScalarPluginBundleVersion.Value + "/index.js";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Enables the gRPC console on a Scalar API Reference resource.
    /// </summary>
    /// <remarks>
    /// The Scalar container HTML cannot inject a configuration global, so the AsyncAPI documents are
    /// passed to the bundle through its own <c>&lt;script&gt;</c> query string
    /// (<c>bundle.js?documents=&lt;base64-json&gt;</c>). This swaps the container's
    /// <see cref="ScalarAspireOptions.BundleUrl" /> for the gRPC-enabled bundle.
    /// The target service must enable gRPC-Web, call <c>MapScalarGrpcAssets()</c> (the console
    /// fetches its protobuf descriptors from the target origin's default assets path), and expose
    /// the gRPC-Web response headers via CORS (<c>Grpc-Status</c>, <c>Grpc-Message</c>,
    /// <c>Grpc-Encoding</c>, <c>Grpc-Accept-Encoding</c>), since the Scalar container's page calls
    /// the service from a different origin.
    /// </remarks>
    /// <param name="builder">The Scalar resource builder.</param>
    /// <param name="configure">Optional callback to override the auto-discovered AsyncAPI documents.</param>
    /// <param name="bundleUrl">The base URL of the gRPC-enabled bundle. Defaults to <see cref="DefaultBundleUrl" />.</param>
    /// <returns>The same resource builder for chaining.</returns>
    public static IResourceBuilder<ScalarResource> WithGrpcClient(
        this IResourceBuilder<ScalarResource> builder,
        Action<ScalarGrpcAspireOptions>? configure = null,
        string bundleUrl = DefaultBundleUrl)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(bundleUrl);

        var options = new ScalarGrpcAspireOptions();
        configure?.Invoke(options);

        var url = bundleUrl;
        if (options.Documents.Count > 0)
        {
            var json = JsonSerializer.Serialize(
                options.Documents.Select(static document => new { name = document.Name, url = document.Url }),
                JsonOptions);
            var encoded = Uri.EscapeDataString(Convert.ToBase64String(Encoding.UTF8.GetBytes(json)));
            var separator = bundleUrl.Contains('?') ? '&' : '?';
            url = $"{bundleUrl}{separator}documents={encoded}";
        }

        builder.ApplicationBuilder.Services.Configure<ScalarAspireOptions>(
            builder.Resource.Name,
            scalarOptions => scalarOptions.BundleUrl = url);

        return builder;
    }
}
