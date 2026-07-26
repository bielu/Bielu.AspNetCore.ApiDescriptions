namespace Bielu.Arazzo.Readers;

public static class ArazzoTextReader
{
    public static ArazzoReadResult Read(TextReader reader, ArazzoReaderSettings? settings = null) =>
        ArazzoStringReader.Read(reader.ReadToEnd(), settings);
}
