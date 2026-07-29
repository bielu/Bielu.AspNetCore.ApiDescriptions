// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bielu.AspNetCore.AsyncApi.Transformers;

namespace Bielu.AspNetCore.AsyncApi.Services;

/// <summary>
/// Runs the <see cref="IAsyncApiSerializedDocumentTransformer"/>s registered on a document, so every
/// place a document is produced — the endpoint and build-time generation — applies them identically.
/// </summary>
internal static class AsyncApiSerializedDocumentPipeline
{
    public static async ValueTask<string> ApplyAsync(
        string serialized,
        AsyncApiOptions options,
        string documentName,
        AsyncApiDocumentFormat format,
        IServiceProvider applicationServices,
        CancellationToken cancellationToken)
    {
        if (options.SerializedDocumentTransformers.Count == 0)
        {
            return serialized;
        }

        var context = new AsyncApiSerializedDocumentContext
        {
            DocumentName = documentName,
            Format = format,
            ApplicationServices = applicationServices
        };

        foreach (var transformer in options.SerializedDocumentTransformers)
        {
            // Each transformer sees the previous one's output, so registration order is application
            // order — the same sequencing an overlay's own actions follow.
            serialized = await transformer.TransformAsync(serialized, context, cancellationToken);
        }

        return serialized;
    }
}
