namespace Bielu.AspNetCore.AsyncApi.Services;

/// <summary>
/// A family of documents, other than AsyncAPI's own, that build-time generation (<c>dotnet getdocument</c>)
/// should list and write through <see cref="Microsoft.Extensions.ApiDescriptions.AsyncApiDocumentProvider"/>.
/// </summary>
/// <remarks>
/// The tool asks a single document provider for every document, so a sibling package such as
/// <c>Bielu.AspNetCore.Arazzo</c> cannot register a provider of its own. It registers a source instead. Sources are
/// enumerated from the container, so the order they, and the AsyncAPI and OpenAPI registrations, were added in
/// does not matter.
/// </remarks>
internal interface IBuildTimeDocumentSource
{
    /// <summary>
    /// The prefix given to a document's build-time name when its name is already taken by another document,
    /// for example <c>arazzo-</c>.
    /// </summary>
    string NamePrefix { get; }

    /// <summary>The documents this source can write.</summary>
    IEnumerable<BuildTimeDocument> GetDocuments();

    /// <summary>Serializes the document called <paramref name="documentName"/> to <paramref name="writer"/>.</summary>
    /// <param name="documentName">The <see cref="BuildTimeDocument.Name"/> of the document.</param>
    /// <param name="writer">The writer to serialize to.</param>
    Task GenerateAsync(string documentName, TextWriter writer);
}

/// <summary>A document a <see cref="IBuildTimeDocumentSource"/> can write.</summary>
/// <param name="Name">The document's own name.</param>
/// <param name="BuildTimeName">The name to list it under, when its owner has chosen one; otherwise <see langword="null"/>.</param>
internal readonly record struct BuildTimeDocument(string Name, string? BuildTimeName = null);
