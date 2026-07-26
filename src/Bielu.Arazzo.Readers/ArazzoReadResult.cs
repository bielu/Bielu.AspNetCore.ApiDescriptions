using Bielu.Arazzo.Models;

namespace Bielu.Arazzo.Readers;

public sealed class ArazzoReadResult
{
    public ArazzoDocument? Document { get; init; }

    public required ArazzoDiagnostics Diagnostics { get; init; }
}
