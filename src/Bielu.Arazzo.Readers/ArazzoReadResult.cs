using Bielu.Arazzo.Models;

namespace Bielu.Arazzo.Readers;

/// <summary>The outcome of reading an Arazzo document: the parsed <see cref="Document"/> (populated with defaults even when errors occurred) plus the collected <see cref="Diagnostics"/>.</summary>
public sealed class ArazzoReadResult
{
    /// <summary>The parsed document. Non-null even when <see cref="Diagnostics"/> reports errors, except when the root itself could not be parsed as JSON/YAML.</summary>
    public ArazzoDocument? Document { get; init; }

    /// <summary>The errors and warnings collected while reading the document.</summary>
    public required ArazzoDiagnostics Diagnostics { get; init; }
}
