// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ByteBard.AsyncAPI.Models;

namespace Bielu.AspNetCore.AsyncApi.Transformers;

internal sealed class DelegateAsyncApiOperationTransformer : IAsyncApiOperationTransformer
{
    private readonly Func<AsyncApiOperation, AsyncApiOperationTransformerContext, CancellationToken, Task> _transformer;

    public DelegateAsyncApiOperationTransformer(Func<AsyncApiOperation, AsyncApiOperationTransformerContext, CancellationToken, Task> transformer)
    {
        _transformer = transformer;
    }

    public async Task TransformAsync(AsyncApiOperation operation, AsyncApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        await _transformer(operation, context, cancellationToken);
    }
}
