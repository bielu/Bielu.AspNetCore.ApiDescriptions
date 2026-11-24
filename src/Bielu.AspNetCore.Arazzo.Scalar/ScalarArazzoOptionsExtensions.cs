using System.Text.Json;
using Bielu.AspNetCore.AsyncApi.Scalar;
using Scalar.AspNetCore;

namespace Bielu.AspNetCore.Arazzo.Scalar;

/// <summary>
/// Extension methods that render Arazzo documents in a Scalar API Reference.
/// </summary>
public static class ScalarArazzoOptionsExtensions
{
    /// <summary>
    /// The window global the Arazzo bundle reads its options from (<see cref="ScalarArazzoOptions" />). The bundle
    /// reads its documents from Scalar's own sources, so it is only written when an option is set.
    /// </summary>
    internal const string GlobalVariableName = "__BIELU_SCALAR_ARAZZO__";

    /// <summary>
    /// Enables Arazzo documents on this Scalar API Reference.
    /// </summary>
    /// <remarks>
    /// This adds a small script (served by <c>MapScalarArazzoAssets</c>) to the page via
    /// <see cref="ScalarOptions.HeadContent" />. The script registers an Arazzo document type with
    /// Scalar's own bundle, so any source that is an Arazzo document renders as its workflows and
    /// steps, and each step links to the operation it calls in the OpenAPI or AsyncAPI documents
    /// loaded next to it. Add the Arazzo documents themselves with
    /// <see cref="AddArazzoDocument" />.
    /// <para>
    /// Plugin document types need <c>Scalar.AspNetCore</c> 3.0.1 or later, which bundles
    /// <c>@scalar/api-reference</c> 1.80.0 (<see href="https://github.com/scalar/scalar/pull/10816">scalar/scalar#10816</see>).
    /// Older versions ignore the plugin and render an Arazzo source as an empty OpenAPI document.
    /// </para>
    /// </remarks>
    /// <param name="options">The Scalar options being configured.</param>
    /// <param name="assetsPath">The base path the Arazzo script is served from. Defaults to <see cref="ScalarArazzoDefaults.AssetsPath" />.</param>
    /// <returns>The same <see cref="ScalarOptions" /> instance for chaining.</returns>
    /// <example>
    /// <code>
    /// app.MapArazzo();
    /// app.MapScalarArazzoAssets();
    /// app.MapScalarApiReference(options => options
    ///     .AddDocument("v1")
    ///     .AddArazzoDocument("v1", "Workflows")
    ///     .WithArazzo());
    /// </code>
    /// </example>
    public static ScalarOptions WithArazzo(this ScalarOptions options, string assetsPath = ScalarArazzoDefaults.AssetsPath)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrEmpty(assetsPath);

        return options.WithAsyncApiPluginScript(assetsPath, GlobalVariableName);
    }

    /// <summary>
    /// Enables Arazzo documents on this Scalar API Reference, with options. Use it in place of
    /// <see cref="WithArazzo(ScalarOptions, string)" />, not as well.
    /// </summary>
    /// <remarks>
    /// Same as <see cref="WithArazzo(ScalarOptions, string)" />; options that change what the view shows are
    /// written to the page as a small configuration script before the Arazzo script. It has a name of its own
    /// rather than being a <c>WithArazzo</c> overload, because <c>WithArazzo(options, assetsPath)</c> already
    /// takes two arguments with an optional one, and a second two-argument overload would make
    /// <c>WithArazzo(null)</c> ambiguous.
    /// </remarks>
    /// <param name="options">The Scalar options being configured.</param>
    /// <param name="configure">Sets the Arazzo view's options.</param>
    /// <returns>The same <see cref="ScalarOptions" /> instance for chaining.</returns>
    /// <example>
    /// <code>
    /// app.MapScalarApiReference(options => options
    ///     .AddArazzoDocument("v1", "Workflows")
    ///     .WithArazzoOptions(arazzo => arazzo.EndLabelExtension = "x-page"));
    /// </code>
    /// </example>
    public static ScalarOptions WithArazzoOptions(this ScalarOptions options, Action<ScalarArazzoOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(configure);

        var arazzo = new ScalarArazzoOptions();
        configure(arazzo);
        ArgumentException.ThrowIfNullOrEmpty(arazzo.AssetsPath, nameof(configure));
        // The options the bundle reads, by their JavaScript names; unset ones are left out.
        var bundleOptions = new Dictionary<string, string>();
        foreach (var (name, value) in new[]
        {
            ("endLabelExtension", arazzo.EndLabelExtension),
            ("pagesExtension", arazzo.PagesExtension),
            ("pageExtension", arazzo.PageExtension),
            ("labelExtension", arazzo.LabelExtension),
            ("actorExtension", arazzo.ActorExtension),
            ("actorsExtension", arazzo.ActorsExtension),
        })
        {
            if (value is null)
            {
                continue;
            }

            // The same rule as the npm package and the Aspire helper: 'x-' followed by a name.
            if (value.Length <= 2 || !value.StartsWith("x-", StringComparison.Ordinal))
            {
                var property = char.ToUpperInvariant(name[0]) + name[1..];
                throw new ArgumentException(
                    $"{property} '{value}' is not a specification extension: it must be 'x-' followed by a name.",
                    nameof(configure));
            }

            bundleOptions[name] = value;
        }

        if (bundleOptions.Count > 0)
        {
            // JsonSerializer escapes <, > and & by default, so a value cannot close the script element.
            var json = JsonSerializer.Serialize(bundleOptions);
            options.HeadContent = (options.HeadContent ?? string.Empty) + $"<script>window.{GlobalVariableName} = {json};</script>";
        }

        return options.WithArazzo(arazzo.AssetsPath);
    }

    /// <summary>
    /// Adds an Arazzo document served by <c>MapArazzo</c> as a source of this Scalar API Reference.
    /// </summary>
    /// <param name="options">The Scalar options being configured.</param>
    /// <param name="documentName">The Arazzo document name passed to <c>AddArazzo</c>; it replaces the <c>{documentName}</c> placeholder in <paramref name="routePattern" />.</param>
    /// <param name="title">The title shown in Scalar's document selector. Defaults to <paramref name="documentName" />.</param>
    /// <param name="routePattern">The route the document is served from. Defaults to <see cref="ArazzoDefaults.DefaultArazzoRoute" />, the <c>MapArazzo</c> default.</param>
    /// <param name="isDefault">Whether this document is selected when the page opens.</param>
    /// <returns>The same <see cref="ScalarOptions" /> instance for chaining.</returns>
    public static ScalarOptions AddArazzoDocument(
        this ScalarOptions options,
        string documentName = ArazzoDefaults.DefaultDocumentName,
        string? title = null,
        string routePattern = ArazzoDefaults.DefaultArazzoRoute,
        bool isDefault = false)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrEmpty(documentName);
        ArgumentException.ThrowIfNullOrEmpty(routePattern);

        return options.AddDocument(documentName, title, routePattern, isDefault);
    }
}
