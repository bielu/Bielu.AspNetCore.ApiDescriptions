using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Scalar.Aspire;

namespace Bielu.AspNetCore.Arazzo.Scalar.Aspire;

/// <summary>
/// Extension methods that render Arazzo documents in a Scalar.Aspire API Reference resource.
/// </summary>
public static class ScalarArazzoAspireExtensions
{
    /// <summary>
    /// The default CDN URL for the published <c>@bielu/scalar-arazzo</c> plugin module. Pinned to the npm
    /// version this package was built against (an immutable jsDelivr asset; the constant is generated from
    /// the npm package.json at build time) so deployed packages always load the plugin released with them.
    /// This must be <c>dist/scalar-plugin.mjs</c>, the ES module whose default export is the plugin.
    /// </summary>
    public const string DefaultPluginUrl =
        "https://cdn.jsdelivr.net/npm/@bielu/scalar-arazzo@" + ScalarPluginBundleVersion.Value + "/dist/scalar-plugin.mjs";

    /// <summary>
    /// The route <c>MapArazzo()</c> serves documents from by default, with the <c>{documentName}</c>
    /// placeholder Scalar replaces with the document name.
    /// </summary>
    public const string DefaultArazzoRoutePattern = "/arazzo/{documentName}.json";

    /// <summary>
    /// Enables the Arazzo document type on a Scalar API Reference resource, so the Arazzo documents added
    /// with <see cref="AddArazzoDocument{TOptions}" /> render as workflows next to the OpenAPI and AsyncAPI
    /// documents their steps call.
    /// </summary>
    /// <remarks>
    /// Registers the plugin through <see cref="ScalarOptions.PluginUrls" />, which Scalar loads with a dynamic
    /// <c>import()</c> before mounting; the container's own Scalar bundle stays in place. The plugin uses
    /// Scalar's plugin <c>documentTypes</c> API, first released in <c>@scalar/api-reference</c> 1.80.0, so the
    /// Scalar container image must bundle that version or later. Older images show an Arazzo source as an
    /// empty OpenAPI document.
    /// </remarks>
    /// <param name="builder">The Scalar resource builder.</param>
    /// <param name="pluginUrl">The URL of the Arazzo plugin module. Defaults to <see cref="DefaultPluginUrl" />.</param>
    /// <returns>The same resource builder for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.AddScalarApiReference()
    ///     .WithArazzo()
    ///     .WithApiReference(api, options => options
    ///         .AddDocument("v1")
    ///         .AddArazzoDocument("v1", "Workflows"));
    /// </code>
    /// </example>
    public static IResourceBuilder<ScalarResource> WithArazzo(
        this IResourceBuilder<ScalarResource> builder,
        string pluginUrl = DefaultPluginUrl)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(pluginUrl);

        builder.ApplicationBuilder.Services.Configure<ScalarAspireOptions>(
            builder.Resource.Name,
            scalarOptions => scalarOptions.AddPluginUrl(pluginUrl));

        return builder;
    }

    /// <summary>
    /// Adds an Arazzo document served by <c>MapArazzo</c> as a source of an API reference, in the
    /// <c>configureOptions</c> callback of <c>WithApiReference</c>.
    /// </summary>
    /// <typeparam name="TOptions">The Scalar options type.</typeparam>
    /// <param name="options">The API reference options being configured.</param>
    /// <param name="documentName">The Arazzo document name passed to <c>AddArazzo</c>; it replaces the <c>{documentName}</c> placeholder in <paramref name="routePattern" />.</param>
    /// <param name="title">The title shown in Scalar's document selector. Defaults to <paramref name="documentName" />.</param>
    /// <param name="routePattern">The route the document is served from. Defaults to <see cref="DefaultArazzoRoutePattern" />, the <c>MapArazzo</c> default.</param>
    /// <param name="isDefault">Whether this document is selected when the page opens.</param>
    /// <returns>The same options instance for chaining.</returns>
    public static TOptions AddArazzoDocument<TOptions>(
        this TOptions options,
        string documentName = "v1",
        string? title = null,
        string routePattern = DefaultArazzoRoutePattern,
        bool isDefault = false)
        where TOptions : ScalarOptions
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrEmpty(documentName);
        ArgumentException.ThrowIfNullOrEmpty(routePattern);

        return options.AddDocument(documentName, title, routePattern, isDefault);
    }
}
