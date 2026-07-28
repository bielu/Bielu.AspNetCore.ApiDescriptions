namespace Bielu.Overlay;

/// <summary>The outcome of applying an overlay: the transformed document plus the diagnostics collected on the way.</summary>
public sealed class OverlayApplyResult
{
    /// <summary>
    /// The transformed document. This is always a distinct tree from the input — <see cref="OverlayApplier"/>
    /// never mutates the caller's document. <see langword="null"/> only when the input was null.
    /// </summary>
    public required System.Text.Json.Nodes.JsonNode? Document { get; init; }

    /// <summary>Errors and warnings collected while applying. Applying is best-effort: a failing action is reported and skipped rather than aborting the rest.</summary>
    public required IReadOnlyList<OverlayDiagnostic> Diagnostics { get; init; }

    /// <summary>Whether any non-warning diagnostic was produced.</summary>
    public bool HasErrors => Diagnostics.Any(d => !d.IsWarning);
}
