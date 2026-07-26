namespace Bielu.Arazzo.Readers;

/// <summary>Reads an Arazzo document from a <see cref="Stream"/>, auto-detecting JSON or YAML.</summary>
public static class ArazzoStreamReader
{
    /// <summary>Reads the Arazzo document produced by exhausting <paramref name="stream"/>.</summary>
    /// <param name="stream">The stream to read the document from; it is read to the end.</param>
    /// <param name="settings">Optional reader settings; defaults are used when omitted.</param>
    /// <returns>The parsed document together with any diagnostics collected while reading it.</returns>
    public static ArazzoReadResult Read(Stream stream, ArazzoReaderSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var reader = new StreamReader(stream);
        return ArazzoStringReader.Read(reader.ReadToEnd(), settings);
    }
}
