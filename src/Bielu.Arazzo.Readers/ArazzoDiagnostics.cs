using Bielu.Arazzo;

namespace Bielu.Arazzo.Readers;

public sealed class ArazzoDiagnostics
{
    public ArazzoVersion Version { get; set; }

    public IList<ArazzoReaderError> Errors { get; } = new List<ArazzoReaderError>();

    public IList<ArazzoReaderError> Warnings { get; } = new List<ArazzoReaderError>();
}
