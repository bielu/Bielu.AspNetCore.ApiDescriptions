namespace Bielu.Overlay;

/// <summary>Options controlling how <see cref="OverlayApplier"/> applies an overlay.</summary>
public sealed class OverlayApplyOptions
{
    /// <summary>
    /// When <see langword="true"/>, a <c>target</c> that selects zero nodes is reported as an error rather
    /// than a warning. Useful in CI to catch overlays that have drifted out of sync with the document they
    /// transform — the spec permits zero matches, so this is a policy choice, not a conformance one.
    /// </summary>
    public bool Strict { get; set; }
}
