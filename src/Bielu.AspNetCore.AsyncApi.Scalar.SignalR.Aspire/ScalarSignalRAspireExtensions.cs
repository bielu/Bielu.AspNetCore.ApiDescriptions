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
    /// The default CDN URL for the published <c>@bielu/scalar-signalr</c> plugin module. Pinned to the
    /// npm version this package was built against (an immutable jsDelivr asset; the constant is
    /// generated from the npm package.json at build time) so deployed packages always load the plugin
    /// released with them rather than floating to whatever is latest on npm. This must be
    /// <c>dist/scalar-plugin.mjs</c>, the ES module whose default export is the plugin — the other
    /// build outputs either self-install via <c>window.Scalar</c> or have no default export, and
    /// Scalar rejects both.
    /// </summary>
    public const string DefaultPluginUrl =
        "https://cdn.jsdelivr.net/npm/@bielu/scalar-signalr@" + ScalarPluginBundleVersion.Value + "/dist/scalar-plugin.mjs";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Enables the SignalR console on a Scalar API Reference resource.
    /// </summary>
    /// <remarks>
    /// Registers the console through <see cref="ScalarOptions.PluginUrls" />, which Scalar loads
    /// with a dynamic <c>import()</c> before mounting. That leaves the container's own Scalar bundle in
    /// place — only the plugin is added.
    /// <para>
    /// The Scalar container HTML is not ours to edit, so it cannot carry a configuration global. When
    /// documents are configured explicitly they travel on the module URL's query string
    /// (<c>scalar-plugin.mjs?documents=&lt;base64-json&gt;</c>), which the module reads from its own
    /// <c>import.meta.url</c>. Without them the console falls back to discovering AsyncAPI documents
    /// from the Scalar configuration it is rendered with.
    /// </para>
    /// </remarks>
    /// <param name="builder">The Scalar resource builder.</param>
    /// <param name="configure">Optional callback to override the auto-discovered AsyncAPI documents.</param>
    /// <param name="pluginUrl">The URL of the SignalR console plugin module. Defaults to <see cref="DefaultPluginUrl" />.</param>
    /// <returns>The same resource builder for chaining.</returns>
    public static IResourceBuilder<ScalarResource> WithSignalRClient(
        this IResourceBuilder<ScalarResource> builder,
        Action<ScalarSignalRAspireOptions>? configure = null,
        string pluginUrl = DefaultPluginUrl)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(pluginUrl);

        var options = new ScalarSignalRAspireOptions();
        configure?.Invoke(options);

        var url = pluginUrl;
        if (options.Documents.Count > 0)
        {
            var json = JsonSerializer.Serialize(
                options.Documents.Select(static document => new { name = document.Name, url = document.Url }),
                JsonOptions);
            var encoded = Uri.EscapeDataString(Convert.ToBase64String(Encoding.UTF8.GetBytes(json)));
            var separator = pluginUrl.Contains('?') ? '&' : '?';
            url = $"{pluginUrl}{separator}documents={encoded}";
        }

        builder.ApplicationBuilder.Services.Configure<ScalarAspireOptions>(
            builder.Resource.Name,
            scalarOptions => scalarOptions.AddPluginUrl(url));

        return builder;
    }
}
