namespace Bielu.Overlay.Readers;

/// <summary>Reads an Overlay document from a <see cref="Stream"/>, auto-detecting JSON or YAML.</summary>
public static class OverlayStreamReader
{
    /// <summary>Reads the Overlay document produced by exhausting <paramref name="stream"/>.</summary>
    /// <param name="stream">The stream to read the document from; it is read to the end.</param>
    /// <param name="settings">Optional reader settings; defaults are used when omitted.</param>
    /// <returns>The parsed document together with any diagnostics collected while reading it.</returns>
    public static OverlayReadResult Read(Stream stream, OverlayReaderSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(stream);

        // leaveOpen: reading a document should not dispose a stream the caller still owns.
        using var reader = new StreamReader(stream, leaveOpen: true);
        return OverlayStringReader.Read(reader.ReadToEnd(), settings);
    }
}
