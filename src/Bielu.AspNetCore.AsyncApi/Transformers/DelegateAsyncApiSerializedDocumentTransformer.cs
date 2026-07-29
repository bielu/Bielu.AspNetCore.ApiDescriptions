// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Bielu.AspNetCore.AsyncApi.Transformers;

internal sealed class DelegateAsyncApiSerializedDocumentTransformer(
    Func<string, AsyncApiSerializedDocumentContext, CancellationToken, ValueTask<string>> transformer)
    : IAsyncApiSerializedDocumentTransformer
{
    public ValueTask<string> TransformAsync(string document, AsyncApiSerializedDocumentContext context, CancellationToken cancellationToken)
        => transformer(document, context, cancellationToken);
}
