namespace Bielu.Arazzo.Readers;

/// <summary>Reads an Arazzo document from a <see cref="TextReader"/>, auto-detecting JSON or YAML.</summary>
public static class ArazzoTextReader
{
    /// <summary>Reads the Arazzo document produced by exhausting <paramref name="reader"/>.</summary>
    /// <param name="reader">The text source to read the document from; it is read to the end.</param>
    /// <param name="settings">Optional reader settings; defaults are used when omitted.</param>
    /// <returns>The parsed document together with any diagnostics collected while reading it.</returns>
    public static ArazzoReadResult Read(TextReader reader, ArazzoReaderSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(reader);

        return ArazzoStringReader.Read(reader.ReadToEnd(), settings);
    }
}
