namespace Bielu.Arazzo.Readers;

/// <summary>A Path/Message pair identifying where in the document a diagnostic occurred.</summary>
public sealed record ArazzoReaderError(string Path, string Message)
{
    /// <inheritdoc />
    public override string ToString() => $"{Path}: {Message}";
}
