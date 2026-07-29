// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Bielu.AspNetCore.AsyncApi.Transformers;

/// <summary>
/// Represents a transformer that rewrites an AsyncApi document *after* it has been serialized, operating
/// on the text a consumer would otherwise have received.
/// </summary>
/// <remarks>
/// <para>
/// This is deliberately a different seam from <see cref="IAsyncApiDocumentTransformer"/>, which hands out
/// a typed <c>AsyncApiDocument</c>. A transformation expressed against the *wire representation* — an
/// OpenAPI Overlay being the motivating case — has no faithful typed equivalent: routing it through the
/// object model would mean serialize, transform, deserialize, and would stake correctness on the
/// underlying serializer round-tripping losslessly. Running at the serialization boundary means the
/// transformer sees exactly the bytes the consumer would have seen.
/// </para>
/// <para>
/// Registered transformers run in registration order, each against the output of the last, at every
/// point a document is produced: the <c>MapAsyncApi</c> endpoint and build-time document generation
/// alike. Prefer <see cref="IAsyncApiDocumentTransformer"/> whenever the change *can* be expressed
/// against the object model — it is typed, cheaper, and cannot produce a malformed document.
/// </para>
/// </remarks>
public interface IAsyncApiSerializedDocumentTransformer
{
    /// <summary>
    /// Transforms the serialized AsyncApi document.
    /// </summary>
    /// <param name="document">The serialized document, in <see cref="AsyncApiSerializedDocumentContext.Format"/>.</param>
    /// <param name="context">The context associated with the <paramref name="document"/>.</param>
    /// <param name="cancellationToken">The cancellation token to use.</param>
    /// <returns>The transformed document, in the same format it was given in.</returns>
    ValueTask<string> TransformAsync(string document, AsyncApiSerializedDocumentContext context,
        CancellationToken cancellationToken);
}
