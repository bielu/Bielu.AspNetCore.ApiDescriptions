// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ByteBard.AsyncAPI.Models;

namespace Bielu.AspNetCore.AsyncApi.Transformers;

/// <summary>
/// Represents a transformer that can be used to modify an AsyncApi schema.
/// </summary>
public interface IAsyncApiSchemaTransformer
{
    /// <summary>
    /// Transforms the specified AsyncApi schema.
    /// </summary>
    /// <param name="schema">The <see cref="AsyncApiJsonSchema"/> to modify.</param>
    /// <param name="context">The <see cref="AsyncApiJsonSchemaTransformerContext"/> associated with the <see paramref="schema"/>.</param>
    /// <param name="cancellationToken">The cancellation token to use.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    Task TransformAsync(AsyncApiJsonSchema schema, AsyncApiJsonSchemaTransformerContext context, CancellationToken cancellationToken);
}
