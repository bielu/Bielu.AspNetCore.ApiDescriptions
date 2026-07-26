namespace Bielu.Arazzo.Readers;

/// <summary>Options controlling how <see cref="ArazzoStringReader"/>, <see cref="ArazzoTextReader"/>, and <see cref="ArazzoStreamReader"/> behave while reading a document.</summary>
public sealed class ArazzoReaderSettings
{
    /// <summary>When true (default), fields not recognized from the spec are captured as extensions silently. When false, each one also produces a warning diagnostic.</summary>
    public bool IgnoreUnrecognizedFields { get; set; } = true;
}
