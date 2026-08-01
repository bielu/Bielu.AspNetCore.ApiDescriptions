// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ByteBard.AsyncAPI.Models;

namespace Bielu.AspNetCore.AsyncApi.Transformers;

/// <summary>
/// Represents a transformer that can be used to modify an AsyncApi operation.
/// </summary>
public interface IAsyncApiOperationTransformer
{
    /// <summary>
    /// Transforms the specified AsyncApi operation.
    /// </summary>
    /// <param name="operation">The <see cref="AsyncApiOperation"/> to modify.</param>
    /// <param name="context">The <see cref="AsyncApiOperationTransformerContext"/> associated with the <paramref name="operation"/>.</param>
    /// <param name="cancellationToken">The cancellation token to use.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    Task TransformAsync(AsyncApiOperation operation, AsyncApiOperationTransformerContext context, CancellationToken cancellationToken);
}
