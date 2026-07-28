namespace Bielu.Overlay.Readers;

/// <summary>Reads an Overlay document from a <see cref="TextReader"/>, auto-detecting JSON or YAML.</summary>
public static class OverlayTextReader
{
    /// <summary>Reads the Overlay document produced by exhausting <paramref name="reader"/>.</summary>
    /// <param name="reader">The text source to read the document from; it is read to the end.</param>
    /// <param name="settings">Optional reader settings; defaults are used when omitted.</param>
    /// <returns>The parsed document together with any diagnostics collected while reading it.</returns>
    public static OverlayReadResult Read(TextReader reader, OverlayReaderSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(reader);

        return OverlayStringReader.Read(reader.ReadToEnd(), settings);
    }
}
