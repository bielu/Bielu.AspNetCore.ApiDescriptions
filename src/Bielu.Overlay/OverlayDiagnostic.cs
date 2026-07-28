namespace Bielu.Overlay;

/// <summary>
/// A finding produced while validating or applying an overlay, located by a JSON-Pointer-style path into
/// the <em>overlay</em> document (e.g. <c>/actions/2/target</c>) rather than into the document being
/// transformed.
/// </summary>
public sealed record OverlayDiagnostic
{
    /// <summary>Creates a diagnostic.</summary>
    /// <param name="path">Where in the overlay document the finding originates.</param>
    /// <param name="message">A human-readable description of the finding.</param>
    /// <param name="isWarning">Whether this is advisory rather than fatal.</param>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> or <paramref name="message"/> is <see langword="null"/>.</exception>
    public OverlayDiagnostic(string path, string message, bool isWarning = false)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(message);

        Path = path;
        Message = message;
        IsWarning = isWarning;
    }

    /// <summary>Where in the overlay document the finding originates.</summary>
    public string Path { get; init; }

    /// <summary>A human-readable description of the finding.</summary>
    public string Message { get; init; }

    /// <summary>Whether this is advisory rather than fatal.</summary>
    public bool IsWarning { get; init; }

    /// <inheritdoc />
    public override string ToString() => $"{(IsWarning ? "warning" : "error")} at {Path}: {Message}";
}
