namespace Bielu.AspNetCore.Arazzo.Scalar;

/// <summary>
/// Options of the Arazzo view in a Scalar API Reference, set with
/// <see cref="ScalarArazzoOptionsExtensions.WithArazzoOptions" />.
/// </summary>
public sealed class ScalarArazzoOptions
{
    /// <summary>The base path the Arazzo script is served from. Defaults to <see cref="ScalarArazzoDefaults.AssetsPath" />.</summary>
    public string AssetsPath { get; set; } = ScalarArazzoDefaults.AssetsPath;

    /// <summary>
    /// A specification extension (such as <c>x-page</c>) on <c>end</c> failure actions whose value names
    /// the end node those actions lead to in the workflow overview, in place of the action's name. Must start with
    /// <c>x-</c>. When unset, each distinct end action gets an end node named after the action.
    /// </summary>
    public string? EndLabelExtension { get; set; }

    /// <summary>
    /// The Pages view: a document-level specification extension (such as <c>x-shop-pages</c>) holding a map of
    /// page id to <c>{ "title", "route", "description" }</c>. Must start with <c>x-</c>.
    /// </summary>
    /// <remarks>
    /// When any of <see cref="PagesExtension" />, <see cref="PageExtension" /> and <see cref="LabelExtension" />
    /// is set and the document uses it, the workflow overview offers a "Technical | Pages" toggle.
    /// </remarks>
    public string? PagesExtension { get; set; }

    /// <summary>
    /// The Pages view: the specification extension (such as <c>x-page</c>) holding a page id on steps (the page the
    /// user is on during the step) and on success and failure actions (the page they land on). It may be the same
    /// as <see cref="EndLabelExtension" />. Must start with <c>x-</c>.
    /// </summary>
    public string? PageExtension { get; set; }

    /// <summary>
    /// The Pages view: the specification extension (such as <c>x-label</c>) holding a plain-language label on
    /// workflows, steps and actions. Must start with <c>x-</c>.
    /// </summary>
    public string? LabelExtension { get; set; }

    /// <summary>
    /// The Sequence view: the specification extension (such as <c>x-actor</c>) on a step or a workflow naming who
    /// makes the step's call: a source description's name (its lane) or anyone else's (a lane of their own). Arazzo
    /// has no such field, since its runner makes every call; when unset, or on steps that do not name one, the call
    /// starts from the client running the workflow. Must start with <c>x-</c>.
    /// </summary>
    public string? ActorExtension { get; set; }
}
