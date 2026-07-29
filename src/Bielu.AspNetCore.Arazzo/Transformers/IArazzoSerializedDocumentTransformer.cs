// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Bielu.AspNetCore.Arazzo.Transformers;

/// <summary>The wire format an Arazzo document was serialized into, and must be re-emitted in.</summary>
public enum ArazzoDocumentFormat
{
    /// <summary>The document was serialized as JSON.</summary>
    Json,

    /// <summary>The document was serialized as YAML.</summary>
    Yaml
}

/// <summary>Represents the context in which an <see cref="IArazzoSerializedDocumentTransformer"/> is executed.</summary>
public sealed class ArazzoSerializedDocumentContext
{
    /// <summary>The name of the associated Arazzo document, lowercased as the rest of the pipeline resolves it.</summary>
    public required string DocumentName { get; init; }

    /// <summary>The format the document was serialized into. A transformer that parses it must re-emit the same format.</summary>
    public required ArazzoDocumentFormat Format { get; init; }

    /// <summary>The request services associated with the current document.</summary>
    public required IServiceProvider ApplicationServices { get; init; }
}

/// <summary>
/// Rewrites an Arazzo document *after* it has been serialized, operating on the text a consumer would
/// otherwise have received.
/// </summary>
/// <remarks>
/// The motivating case is an OpenAPI Overlay, whose transformations are expressed as JSONPath queries over
/// the wire representation and have no faithful equivalent against the typed object model. Registered
/// transformers run in registration order, each against the output of the last.
/// </remarks>
public interface IArazzoSerializedDocumentTransformer
{
    /// <summary>Transforms the serialized Arazzo document.</summary>
    /// <param name="document">The serialized document, in <see cref="ArazzoSerializedDocumentContext.Format"/>.</param>
    /// <param name="context">The context associated with the document.</param>
    /// <param name="cancellationToken">The cancellation token to use.</param>
    /// <returns>The transformed document, in the same format it was given in.</returns>
    ValueTask<string> TransformAsync(string document, ArazzoSerializedDocumentContext context, CancellationToken cancellationToken);
}
