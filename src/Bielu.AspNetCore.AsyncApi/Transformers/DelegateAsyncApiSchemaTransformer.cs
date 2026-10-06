// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ByteBard.AsyncAPI.Models;

namespace Bielu.AspNetCore.AsyncApi.Transformers;

internal sealed class DelegateAsyncApiSchemaTransformer : IAsyncApiSchemaTransformer
{
    private readonly Func<AsyncApiJsonSchema, AsyncApiJsonSchemaTransformerContext, CancellationToken, Task> _transformer;

    public DelegateAsyncApiSchemaTransformer(Func<AsyncApiJsonSchema, AsyncApiJsonSchemaTransformerContext, CancellationToken, Task> transformer)
    {
        _transformer = transformer;
    }

    public async Task TransformAsync(AsyncApiJsonSchema schema, AsyncApiJsonSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        await _transformer(schema, context, cancellationToken);
    }
}
