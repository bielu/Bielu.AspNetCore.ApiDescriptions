using System.Text;
using System.Text.Json;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Scalar.Aspire;

namespace Bielu.AspNetCore.AsyncApi.Scalar.SignalR.Aspire;

/// <summary>
/// Extension methods that add the interactive SignalR console to a Scalar.Aspire resource.
/// </summary>
public static class ScalarSignalRAspireExtensions
{
    /// <summary>
    /// The default CDN URL for the published <c>@bielu/scalar-signalr</c> bundle.
    /// </summary>
    public const string DefaultBundleUrl = "https://cdn.jsdelivr.net/npm/@bielu/scalar-signalr";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Enables the SignalR console on a Scalar API Reference resource.
    /// </summary>
    /// <remarks>
    /// The Scalar container HTML cannot inject a configuration global, so the AsyncAPI documents are
    /// passed to the bundle through its own <c>&lt;script&gt;</c> query string
    /// (<c>bundle.js?documents=&lt;base64-json&gt;</c>). This swaps the container's
    /// <see cref="ScalarAspireOptions.BundleUrl" /> for the SignalR-enabled bundle.
    /// </remarks>
    /// <param name="builder">The Scalar resource builder.</param>
    /// <param name="configure">Optional callback to override the auto-discovered AsyncAPI documents.</param>
    /// <param name="bundleUrl">The base URL of the SignalR-enabled bundle. Defaults to <see cref="DefaultBundleUrl" />.</param>
    /// <returns>The same resource builder for chaining.</returns>
    public static IResourceBuilder<ScalarResource> WithSignalRClient(
        this IResourceBuilder<ScalarResource> builder,
        Action<ScalarSignalRAspireOptions>? configure = null,
        string bundleUrl = DefaultBundleUrl)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(bundleUrl);

        var options = new ScalarSignalRAspireOptions();
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
