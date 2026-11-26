// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ByteBard.AsyncAPI.Models;

namespace Bielu.AspNetCore.AsyncApi.Transformers;

internal sealed class DelegateAsyncApiDocumentTransformer(
    Func<AsyncApiDocument, AsyncApiDocumentTransformerContext, CancellationToken, Task> transformer)
    : IAsyncApiDocumentTransformer
{
    public Task TransformAsync(AsyncApiDocument document, AsyncApiDocumentTransformerContext context, CancellationToken cancellationToken)
        => transformer(document, context, cancellationToken);
}
