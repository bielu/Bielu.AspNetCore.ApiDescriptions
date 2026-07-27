using Bielu.Overlay.Models;

namespace Bielu.Overlay.Readers;

/// <summary>The outcome of reading an Overlay document: the parsed <see cref="Document"/> plus the collected <see cref="Diagnostics"/>.</summary>
public sealed class OverlayReadResult
{
    /// <summary>
    /// The parsed document. Non-null even when <see cref="Diagnostics"/> reports errors — required fields
    /// are filled with defaults so partial results stay inspectable — except when the input could not be
    /// parsed as JSON or YAML at all.
    /// </summary>
    public OverlayDocument? Document { get; init; }

    /// <summary>The errors and warnings collected while reading the document.</summary>
    public required IReadOnlyList<OverlayDiagnostic> Diagnostics { get; init; }

    /// <summary>Whether any non-warning diagnostic was produced.</summary>
    public bool HasErrors => Diagnostics.Any(d => !d.IsWarning);
}
