// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Bielu.AspNetCore.AsyncApi.Transformers;

/// <summary>
/// Represents the context in which an <see cref="IAsyncApiSerializedDocumentTransformer"/> is executed.
/// </summary>
public sealed class AsyncApiSerializedDocumentContext
{
    /// <summary>
    /// Gets the name of the associated AsyncApi document, already lowercased the way the rest of the
    /// pipeline resolves document names.
    /// </summary>
    public required string DocumentName { get; init; }

    /// <summary>
    /// Gets the format the document was serialized into. A transformer that parses the document must
    /// re-emit it in the same format.
    /// </summary>
    public required AsyncApiDocumentFormat Format { get; init; }

    /// <summary>
    /// Gets the application services associated with the current document. Scoped to the request when
    /// the document is being served over HTTP.
    /// </summary>
    public required IServiceProvider ApplicationServices { get; init; }
}
