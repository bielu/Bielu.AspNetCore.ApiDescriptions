using Bielu.Arazzo.Models;

namespace Bielu.AspNetCore.Arazzo.Transformers;

/// <summary>Represents the context in which an <see cref="IArazzoDocumentTransformer"/> is executed.</summary>
public sealed class ArazzoDocumentTransformerContext
{
    /// <summary>The name of the associated Arazzo document.</summary>
    public required string DocumentName { get; init; }

    /// <summary>The application services, for transformers that need registered services.</summary>
    public required IServiceProvider ApplicationServices { get; init; }
}

/// <summary>
/// Modifies an Arazzo document on its object model, after <c>AddArazzo</c>'s options have built it and
/// <em>before</em> it is validated, so whatever a transformer adds is validated like the rest.
/// </summary>
/// <remarks>
/// Transformers run once per document, in registration order, when the document is first built; the result is
/// cached. To rewrite the serialized text instead, as an OpenAPI Overlay does, use
/// <see cref="IArazzoSerializedDocumentTransformer"/>.
/// </remarks>
/// <example>
/// <code>
/// options.AddDocumentTransformer((document, context, cancellationToken) =>
/// {
///     document.Info.Description = $"Workflows of {context.DocumentName}";
///     return Task.CompletedTask;
/// });
/// </code>
/// </example>
public interface IArazzoDocumentTransformer
{
    /// <summary>Transforms the specified Arazzo document.</summary>
    /// <param name="document">The <see cref="ArazzoDocument"/> to modify.</param>
    /// <param name="context">The context associated with the document.</param>
    /// <param name="cancellationToken">The cancellation token to use.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    Task TransformAsync(ArazzoDocument document, ArazzoDocumentTransformerContext context, CancellationToken cancellationToken);
}
