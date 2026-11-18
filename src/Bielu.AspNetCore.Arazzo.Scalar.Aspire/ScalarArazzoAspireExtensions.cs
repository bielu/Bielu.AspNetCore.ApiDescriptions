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
    /// Enables the Arazzo document type on a Scalar API Reference resource, like <see cref="WithArazzo" />, with
    /// options that change what the view shows. Use it instead of <see cref="WithArazzo" />, not with it.
    /// </summary>
    /// <remarks>
    /// The plugin module reads its options from its own URL, since <see cref="ScalarOptions.PluginUrls" /> has no
    /// other way to pass them: each one set is added to the query string of
    /// <see cref="ScalarArazzoAspireOptions.PluginUrl" /> (<c>scalar-plugin.mjs?pageExtension=x-page</c>), keeping
    /// that URL's other query parameters and fragment. With none set, the URL is used as it is.
    /// </remarks>
    /// <param name="builder">The Scalar resource builder.</param>
    /// <param name="configure">Sets the Arazzo view's options.</param>
    /// <returns>The same resource builder for chaining.</returns>
    /// <exception cref="ArgumentException">An extension name is not a specification extension (<c>x-</c> followed by a name).</exception>
    /// <example>
    /// <code>
    /// builder.AddScalarApiReference()
    ///     .WithArazzoOptions(arazzo =>
    ///     {
    ///         arazzo.PagesExtension = "x-shop-pages";
    ///         arazzo.PageExtension = "x-page";
    ///         arazzo.LabelExtension = "x-label";
    ///     })
    ///     .WithApiReference(api, options => options.AddArazzoDocument("v1", "Workflows"));
    /// </code>
    /// </example>
    public static IResourceBuilder<ScalarResource> WithArazzoOptions(
        this IResourceBuilder<ScalarResource> builder,
        Action<ScalarArazzoAspireOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var arazzo = new ScalarArazzoAspireOptions();
        configure(arazzo);
        ArgumentException.ThrowIfNullOrEmpty(arazzo.PluginUrl, nameof(configure));

        // The options the module reads, by their JavaScript names; unset ones are left out.
        var moduleOptions = new List<KeyValuePair<string, string>>();
        foreach (var (name, value) in new[]
        {
            (nameof(ScalarArazzoAspireOptions.EndLabelExtension), arazzo.EndLabelExtension),
            (nameof(ScalarArazzoAspireOptions.PagesExtension), arazzo.PagesExtension),
            (nameof(ScalarArazzoAspireOptions.PageExtension), arazzo.PageExtension),
            (nameof(ScalarArazzoAspireOptions.LabelExtension), arazzo.LabelExtension),
        })
        {
            if (value is null)
            {
                continue;
            }

            if (!IsSpecificationExtension(value))
            {
                throw new ArgumentException(
                    $"{name} '{value}' is not a specification extension: it must be 'x-' followed by a name.",
                    nameof(configure));
            }

            moduleOptions.Add(new(char.ToLowerInvariant(name[0]) + name[1..], value));
        }

        return builder.WithArazzo(WithQuery(arazzo.PluginUrl, moduleOptions));
    }

    /// <summary>
    /// Whether a name is a specification extension: <c>x-</c> followed by at least one character. The npm
    /// package and <c>Bielu.AspNetCore.Arazzo.Scalar</c> apply the same rule.
    /// </summary>
    internal static bool IsSpecificationExtension(string value) =>
        value.Length > 2 && value.StartsWith("x-", StringComparison.Ordinal);

    /// <summary>
    /// Sets <paramref name="parameters" /> in the query string of <paramref name="url" />, replacing any of the
    /// same name and keeping the others and the fragment. A URL without parameters to set is returned as it is.
    /// </summary>
    internal static string WithQuery(string url, IReadOnlyList<KeyValuePair<string, string>> parameters)
    {
        if (parameters.Count == 0)
        {
            return url;
        }

        var hash = url.IndexOf('#', StringComparison.Ordinal);
        var fragment = hash >= 0 ? url[hash..] : string.Empty;
        var beforeFragment = hash >= 0 ? url[..hash] : url;
        var question = beforeFragment.IndexOf('?', StringComparison.Ordinal);
        var path = question >= 0 ? beforeFragment[..question] : beforeFragment;
        var query = question >= 0 ? beforeFragment[(question + 1)..] : string.Empty;

        var names = parameters.Select(parameter => parameter.Key).ToHashSet(StringComparer.Ordinal);
        var kept = query
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(pair => !names.Contains(Uri.UnescapeDataString(pair.Split('=', 2)[0])));
        var added = parameters.Select(parameter => $"{Uri.EscapeDataString(parameter.Key)}={Uri.EscapeDataString(parameter.Value)}");

        return $"{path}?{string.Join('&', kept.Concat(added))}{fragment}";
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
