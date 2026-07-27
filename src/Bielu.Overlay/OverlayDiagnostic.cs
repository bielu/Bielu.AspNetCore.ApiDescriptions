namespace Bielu.Overlay;

/// <summary>
/// A finding produced while validating or applying an overlay, located by a JSON-Pointer-style path into
/// the <em>overlay</em> document (e.g. <c>/actions/2/target</c>) rather than into the document being
/// transformed.
/// </summary>
/// <param name="Path">Where in the overlay document the finding originates.</param>
/// <param name="Message">A human-readable description of the finding.</param>
/// <param name="IsWarning">Whether this is advisory rather than fatal.</param>
public sealed record OverlayDiagnostic(string Path, string Message, bool IsWarning = false)
{
    /// <inheritdoc />
    public override string ToString() => $"{(IsWarning ? "warning" : "error")} at {Path}: {Message}";
}
