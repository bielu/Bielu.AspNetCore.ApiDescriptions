// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ByteBard.AsyncAPI.Models;

namespace Bielu.AspNetCore.AsyncApi.Services;

/// <summary>
/// Represents a provider for AsyncApi documents that can be used by consumers to
/// retrieve generated AsyncApi documents at runtime.
/// </summary>
public interface IAsyncApiDocumentProvider
{
    /// <summary>
    /// Gets the AsyncApi document.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the AsyncApi document.</returns>
    /// <remarks>
    /// This method is typically used by consumers to retrieve the AsyncApi document. The generated document
    /// may not contain the appropriate servers information since it can be instantiated outside the context
    /// of an HTTP request. In these scenarios, the <see cref="AsyncApiDocument"/> can be modified to
    /// include the appropriate servers information.
    /// </remarks>
    /// <remarks>
    /// Any AsyncApi transformers registered in the <see cref="AsyncApiOptions"/> instance associated with
    /// this document will be applied to the document before it is returned.
    /// </remarks>
    Task<AsyncApiDocument> GetAsyncApiDocumentAsync(CancellationToken cancellationToken = default);
}
