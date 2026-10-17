namespace Bielu.AspNetCore.ApiDescriptions;

/// <summary>
/// A family of documents, such as AsyncAPI or Arazzo, that build-time generation (<c>dotnet getdocument</c>)
/// should list and write.
/// </summary>
/// <remarks>
/// The tool asks a single document provider for every document, so each package registers a source instead of a
/// provider of its own (see <see cref="BuildTimeDocumentServiceCollectionExtensions.AddBuildTimeDocumentSource{TSource}"/>).
/// <c>Microsoft.AspNetCore.OpenApi</c>'s documents are always named first and keep their names. Sources are named
/// after them, in ascending <see cref="Order"/>: a document keeps its own name unless an OpenAPI document, a document
/// of a source with a lower order, or a document of another source with the same order already uses it, in which
/// case it gets its source's <see cref="NamePrefix"/>. Registration order never changes the result.
/// </remarks>
/// <example>
/// <code>
/// internal sealed class WorkflowDocumentSource(WorkflowStore store) : IBuildTimeDocumentSource
/// {
///     public string Kind => "Workflow";
///     public string NamePrefix => "workflow-";
///     public int Order => 100;
///     public IEnumerable&lt;BuildTimeDocument&gt; GetDocuments() =>
///         store.Names.Select(name => new BuildTimeDocument(name));
///     public Task GenerateAsync(string documentName, TextWriter writer) =>
///         writer.WriteAsync(store.Serialize(documentName));
/// }
/// </code>
/// </example>
public interface IBuildTimeDocumentSource
{
    /// <summary>The kind of document this source writes, for example <c>AsyncAPI</c>, used in error messages.</summary>
    string Kind { get; }

    /// <summary>
    /// The prefix given to a document's build-time name when its name is already taken by another document,
    /// for example <c>arazzo-</c>.
    /// </summary>
    string NamePrefix { get; }

    /// <summary>
    /// When this source is named relative to the others. Sources with a lower order are named first, so their
    /// documents keep their names when a later source's document would collide with them. AsyncAPI uses <c>0</c>.
    /// </summary>
    int Order { get; }

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
public readonly record struct BuildTimeDocument(string Name, string? BuildTimeName = null);
