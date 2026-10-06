// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bielu.AspNetCore.AsyncApi.Services;
using ByteBard.AsyncAPI.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Bielu.AspNetCore.AsyncApi.Transformers;

internal sealed class DelegateAsyncApiDocumentTransformer : IAsyncApiDocumentTransformer
{
    private readonly Func<AsyncApiDocument, AsyncApiDocumentTransformerContext, CancellationToken, Task>? _documentTransformer;
    private readonly Func<AsyncApiOperation, AsyncApiOperationTransformerContext, CancellationToken, Task>? _operationTransformer;

    public DelegateAsyncApiDocumentTransformer(Func<AsyncApiDocument, AsyncApiDocumentTransformerContext, CancellationToken, Task> transformer)
    {
        _documentTransformer = transformer;
    }

    public DelegateAsyncApiDocumentTransformer(Func<AsyncApiOperation, AsyncApiOperationTransformerContext, CancellationToken, Task> transformer)
    {
        _operationTransformer = transformer;
    }

    public async Task TransformAsync(AsyncApiDocument document, AsyncApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        if (_documentTransformer != null)
        {
            await _documentTransformer(document, context, cancellationToken);
        }

        if (_operationTransformer != null)
        {
            var documentService = context.ApplicationServices.GetRequiredKeyedService<AsyncApiDocumentService>(context.DocumentName);
            // await documentService.ForEachOperationAsync(
            //     document,
            //     async (operation, operationContext, token) => await _operationTransformer(operation, operationContext, token),
            //     cancellationToken);
        }
    }
}
