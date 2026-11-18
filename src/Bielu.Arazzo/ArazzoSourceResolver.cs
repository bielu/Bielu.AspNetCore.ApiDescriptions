using System.Text.Json.Nodes;

namespace Bielu.Arazzo;

/// <summary>
/// Base class for an <see cref="IArazzoSourceResolver"/> that reads documents of type <typeparamref name="TDocument"/>.
/// The untyped interface methods check the document's type and call the typed overrides, so a resolver never
/// casts, and a document registered under the wrong source type fails with a clear error instead of resolving
/// nothing.
/// </summary>
/// <typeparam name="TDocument">The document type, such as <c>AsyncApiDocument</c> or <c>OpenApiDocument</c>.</typeparam>
/// <example>
/// <code>
/// public sealed class MyApiSourceResolver : ArazzoSourceResolver&lt;MyApiDocument&gt;
/// {
///     public override string SourceType => "myapi";
///
///     public override bool TryResolveOperation(MyApiDocument document, string operationId, out JsonNode? operation)
///     {
///         operation = document.FindOperation(operationId);
///         return operation is not null;
///     }
/// }
/// </code>
/// </example>
public abstract class ArazzoSourceResolver<TDocument> : IArazzoSourceResolver
    where TDocument : class
{
    /// <inheritdoc />
    public abstract string SourceType { get; }

    /// <inheritdoc />
    public Type DocumentType => typeof(TDocument);

    /// <summary>Attempts to resolve an operation by its ID in <paramref name="document"/>.</summary>
    /// <param name="document">The source document.</param>
    /// <param name="operationId">The operation's ID.</param>
    /// <param name="operation">The operation, as JSON, when it resolves.</param>
    /// <returns><see langword="true"/> when the operation exists.</returns>
    public abstract bool TryResolveOperation(TDocument document, string operationId, out JsonNode? operation);

    /// <summary>
    /// Attempts to resolve an operation by its JSON Pointer in <paramref name="document"/>. Returns
    /// <see langword="false"/> by default, for document types without pointer-addressed operations.
    /// </summary>
    /// <param name="document">The source document.</param>
    /// <param name="jsonPointer">The JSON Pointer, without the leading <c>#</c>.</param>
    /// <param name="operation">The operation, as JSON, when it resolves.</param>
    /// <returns><see langword="true"/> when the operation exists.</returns>
    public virtual bool TryResolveOperationPath(TDocument document, string jsonPointer, out JsonNode? operation)
    {
        operation = null;
        return false;
    }

    /// <summary>
    /// Attempts to resolve a channel by its JSON Pointer in <paramref name="document"/>. Returns
    /// <see langword="false"/> by default, for document types without channels.
    /// </summary>
    /// <param name="document">The source document.</param>
    /// <param name="jsonPointer">The JSON Pointer, without the leading <c>#</c>.</param>
    /// <param name="channel">The channel, as JSON, when it resolves.</param>
    /// <returns><see langword="true"/> when the channel exists.</returns>
    public virtual bool TryResolveChannelPath(TDocument document, string jsonPointer, out JsonNode? channel)
    {
        channel = null;
        return false;
    }

    bool IArazzoSourceResolver.TryResolveOperation(object document, string operationId, out JsonNode? operation)
    {
        ArgumentNullException.ThrowIfNull(operationId);
        return TryResolveOperation(Cast(document), operationId, out operation);
    }

    bool IArazzoSourceResolver.TryResolveOperationPath(object document, string jsonPointer, out JsonNode? operation)
    {
        ArgumentNullException.ThrowIfNull(jsonPointer);
        return TryResolveOperationPath(Cast(document), jsonPointer, out operation);
    }

    bool IArazzoSourceResolver.TryResolveChannelPath(object document, string jsonPointer, out JsonNode? channel)
    {
        ArgumentNullException.ThrowIfNull(jsonPointer);
        return TryResolveChannelPath(Cast(document), jsonPointer, out channel);
    }

    private TDocument Cast(object document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return document as TDocument ?? throw new ArgumentException(
            $"The '{SourceType}' resolver reads {typeof(TDocument).Name} documents, but was given a {document.GetType().Name}. " +
            "Register the document under the source type of the resolver that reads it.",
            nameof(document));
    }
}
