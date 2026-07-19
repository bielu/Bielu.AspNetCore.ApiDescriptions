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
    /// The default CDN URL for the published <c>@bielu/scalar-grpc</c> bundle. Pinned to a specific
    /// published version (an immutable jsDelivr asset) so deployed packages always load the same bundle
    /// rather than floating to whatever is latest on npm.
    /// </summary>
    public const string DefaultBundleUrl = "https://cdn.jsdelivr.net/npm/@bielu/scalar-grpc@0.1.0";

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
