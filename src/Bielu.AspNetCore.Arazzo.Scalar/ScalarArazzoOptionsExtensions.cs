using Bielu.AspNetCore.AsyncApi.Scalar;
using Scalar.AspNetCore;

namespace Bielu.AspNetCore.Arazzo.Scalar;

/// <summary>
/// Extension methods that render Arazzo documents in a Scalar API Reference.
/// </summary>
public static class ScalarArazzoOptionsExtensions
{
    /// <summary>
    /// The window global reserved for explicit configuration of the Arazzo bundle. The bundle reads
    /// its documents from Scalar's own sources, so nothing is assigned to it yet.
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
    /// Plugin document types need a Scalar version that includes
    /// <see href="https://github.com/scalar/scalar/pull/10816">scalar/scalar#10816</see>. Older
    /// versions ignore the plugin and render an Arazzo source as an empty OpenAPI document.
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
