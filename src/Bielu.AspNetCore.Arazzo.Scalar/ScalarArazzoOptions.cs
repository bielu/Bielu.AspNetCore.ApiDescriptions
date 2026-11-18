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
}
