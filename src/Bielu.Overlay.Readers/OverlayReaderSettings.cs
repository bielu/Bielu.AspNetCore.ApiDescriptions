namespace Bielu.Overlay.Readers;

/// <summary>Options controlling how <see cref="OverlayStringReader"/>, <see cref="OverlayTextReader"/>, and <see cref="OverlayStreamReader"/> behave while reading a document.</summary>
public sealed class OverlayReaderSettings
{
    /// <summary>When true (default), fields not recognized from the spec are captured as extensions silently. When false, each one also produces a warning diagnostic.</summary>
    public bool IgnoreUnrecognizedFields { get; set; } = true;
}
