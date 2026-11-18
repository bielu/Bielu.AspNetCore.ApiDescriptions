namespace Bielu.AspNetCore.Arazzo.Scalar.Aspire;

/// <summary>
/// Options of the Arazzo view in a Scalar.Aspire API Reference resource, set with
/// <see cref="ScalarArazzoAspireExtensions.WithArazzoOptions" />. The extension names mirror
/// <c>ScalarArazzoOptions</c> in <c>Bielu.AspNetCore.Arazzo.Scalar</c> and <c>createArazzoPlugin</c> in
/// <c>@bielu/scalar-arazzo</c>; each must be a specification extension: <c>x-</c> followed by a name.
/// </summary>
public sealed class ScalarArazzoAspireOptions
{
    /// <summary>
    /// The URL of the Arazzo plugin module. Defaults to <see cref="ScalarArazzoAspireExtensions.DefaultPluginUrl" />.
    /// The options are added to its query string; its other query parameters and its fragment are kept.
    /// </summary>
    public string PluginUrl { get; set; } = ScalarArazzoAspireExtensions.DefaultPluginUrl;

    /// <summary>
    /// A specification extension (such as <c>x-page</c>) on <c>end</c> failure actions whose value names the end
    /// node those actions lead to in the workflow overview, in place of the action's name.
    /// </summary>
    public string? EndLabelExtension { get; set; }

    /// <summary>
    /// The Pages view: a document-level specification extension (such as <c>x-shop-pages</c>) holding a map of
    /// page id to <c>{ "title", "route", "description" }</c>.
    /// </summary>
    public string? PagesExtension { get; set; }

    /// <summary>
    /// The Pages view: the specification extension (such as <c>x-page</c>) holding a page id on steps and on
    /// success and failure actions.
    /// </summary>
    public string? PageExtension { get; set; }

    /// <summary>
    /// The Pages view: the specification extension (such as <c>x-label</c>) holding a plain-language label on
    /// workflows, steps and actions.
    /// </summary>
    public string? LabelExtension { get; set; }
}
