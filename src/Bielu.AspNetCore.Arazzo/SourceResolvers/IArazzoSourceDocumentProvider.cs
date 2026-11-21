using Bielu.Arazzo;

namespace Bielu.AspNetCore.Arazzo.SourceResolvers;

/// <summary>
/// Supplies the document a self-wired source description points at, so startup validation can resolve a step's
/// references against it. Where an <see cref="IArazzoSourceResolver"/> looks <em>inside</em> a document, this
/// finds the document in the first place.
/// </summary>
/// <remarks>
/// <para>
/// <c>AddArazzo</c> registers providers for <c>openapi</c> (documents added with <c>AddOpenApi</c>) and
/// <c>asyncapi</c> (documents added with <c>AddAsyncApi</c>). Several providers may handle the same type: they are
/// asked in reverse registration order, and the first that returns a document wins. A provider added after
/// <c>AddArazzo</c> therefore goes first, and by returning <see langword="null"/> for names it does not own it
/// leaves them to the built-in one.
/// </para>
/// <para>
/// The document a provider returns is read by the <see cref="IArazzoSourceResolver"/> registered for the same
/// type, so it must be of that resolver's <see cref="IArazzoSourceResolver.DocumentType"/>: an
/// <c>OpenApiDocument</c> for <c>openapi</c>, an <c>AsyncApiDocument</c> for <c>asyncapi</c>.
/// </para>
/// <para>Providers are resolved from the root service provider, so register them as singletons.</para>
/// </remarks>
/// <example>
/// A provider that serves a partner's OpenAPI description from a file shipped with the app, next to the app's own
/// documents:
/// <code>
/// services.TryAddEnumerable(ServiceDescriptor.Singleton&lt;IArazzoSourceDocumentProvider, PartnerOpenApiProvider&gt;());
/// services.AddArazzo(options => options
///     .AddOpenApiSource("orders", "v1")
///     .AddSelfWiredSource("payments", ArazzoSourceDescriptionType.OpenApi, "payments", "https://payments.example.com/openapi.json"));
///
/// sealed class PartnerOpenApiProvider : IArazzoSourceDocumentProvider
/// {
///     public string SourceType => ArazzoSourceDescriptionType.OpenApi.Value;
///
///     public async ValueTask&lt;object?&gt; GetDocumentAsync(string documentName, CancellationToken cancellationToken)
///     {
///         if (documentName != "payments")
///         {
///             return null; // not ours: the built-in provider looks it up among AddOpenApi documents
///         }
///
///         await using var stream = File.OpenRead("payments.openapi.json");
///         var result = await OpenApiDocument.LoadAsync(stream, "json", null, cancellationToken);
///         return result.Document;
///     }
/// }
/// </code>
/// </example>
public interface IArazzoSourceDocumentProvider
{
    /// <summary>
    /// The source-description <c>type</c> this provider supplies documents for, such as <c>"openapi"</c>. It
    /// matches the <see cref="IArazzoSourceResolver.SourceType"/> of the resolver that reads those documents.
    /// </summary>
    string SourceType { get; }

    /// <summary>
    /// Returns the document known to this provider as <paramref name="documentName"/>, or <see langword="null"/>
    /// when it has none, which passes the name on to the next provider for the type. When no provider returns a
    /// document, startup validation reports an error naming the Arazzo document, the source and
    /// <paramref name="documentName"/>.
    /// </summary>
    /// <param name="documentName">The document name the source was self-wired with.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    ValueTask<object?> GetDocumentAsync(string documentName, CancellationToken cancellationToken);
}
