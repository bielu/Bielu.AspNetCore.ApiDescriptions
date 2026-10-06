// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ByteBard.AsyncAPI.Models;

namespace Bielu.AspNetCore.AsyncApi.Transformers;

/// <summary>
/// Represents a transformer that can be used to modify an AsyncApi document.
/// </summary>
public interface IAsyncApiDocumentTransformer
{
    /// <summary>
    /// Transforms the specified AsyncApi document.
    /// </summary>
    /// <param name="document">The <see cref="AsyncApiDocument"/> to modify.</param>
    /// <param name="context">The <see cref="AsyncApiDocumentTransformerContext"/> associated with the <see paramref="document"/>.</param>
    /// <param name="cancellationToken">The cancellation token to use.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    Task TransformAsync(AsyncApiDocument document, AsyncApiDocumentTransformerContext context, CancellationToken cancellationToken);
}
