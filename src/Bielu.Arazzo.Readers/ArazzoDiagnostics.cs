using Bielu.Arazzo;

namespace Bielu.Arazzo.Readers;

/// <summary>Collects the outcome of reading an Arazzo document: the detected specification version, plus any errors and warnings.</summary>
public sealed class ArazzoDiagnostics
{
    /// <summary>The Arazzo Specification version detected from the document's <c>arazzo</c> field.</summary>
    public ArazzoVersion Version { get; set; }

    /// <summary>Problems severe enough that the resulting document may be incomplete or incorrect.</summary>
    public IList<ArazzoReaderError> Errors { get; } = new List<ArazzoReaderError>();

    /// <summary>Non-fatal issues, such as unrecognized fields or an unrecognized Arazzo version.</summary>
    public IList<ArazzoReaderError> Warnings { get; } = new List<ArazzoReaderError>();
}
