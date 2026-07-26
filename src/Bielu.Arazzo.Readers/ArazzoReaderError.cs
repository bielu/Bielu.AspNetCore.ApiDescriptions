namespace Bielu.Arazzo.Readers;

public sealed record ArazzoReaderError(string Path, string Message)
{
    public override string ToString() => $"{Path}: {Message}";
}
