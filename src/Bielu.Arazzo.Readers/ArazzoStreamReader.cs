namespace Bielu.Arazzo.Readers;

public static class ArazzoStreamReader
{
    public static ArazzoReadResult Read(Stream stream, ArazzoReaderSettings? settings = null)
    {
        using var reader = new StreamReader(stream);
        return ArazzoStringReader.Read(reader.ReadToEnd(), settings);
    }
}
