// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bielu.AspNetCore.AsyncApi.Transformers;
using ByteBard.AsyncAPI.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Bielu.AspNetCore.AsyncApi.Extensions;

/// <summary>
/// Extension methods for annotating AsyncApi descriptions on an <see cref="Endpoint" />.
/// </summary>
public static class AsyncApiEndpointConventionBuilderExtensions
{
    /// <summary>
    /// Adds an AsyncApi operation transformer to the <see cref="EndpointBuilder.Metadata" /> associated
    /// with the current endpoint.
    /// </summary>
    /// <param name="builder">The <see cref="IEndpointConventionBuilder"/>.</param>
    /// <param name="transformer">The <see cref="Func{AsyncApiOperation, AsyncApiOperationTransformerContext, CancellationToken, Task}"/> that modifies the operation in the <see cref="AsyncApiDocument"/>.</param>
    /// <returns>A <see cref="IEndpointConventionBuilder"/> that can be used to further customize the endpoint.</returns>
    public static TBuilder AddAsyncApiOperationTransformer<TBuilder>(this TBuilder builder, Func<AsyncApiOperation, AsyncApiOperationTransformerContext, CancellationToken, Task> transformer) where TBuilder : IEndpointConventionBuilder
    {
        builder.WithMetadata(new DelegateAsyncApiOperationTransformer(transformer));
        return builder;
    }
}
