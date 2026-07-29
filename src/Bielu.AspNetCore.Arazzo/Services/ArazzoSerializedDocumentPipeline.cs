// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bielu.AspNetCore.Arazzo.Transformers;

namespace Bielu.AspNetCore.Arazzo.Services;

/// <summary>
/// Runs the <see cref="IArazzoSerializedDocumentTransformer"/>s registered on a document, mirroring the
/// AsyncAPI core package's equivalent seam.
/// </summary>
internal static class ArazzoSerializedDocumentPipeline
{
    public static async ValueTask<string> ApplyAsync(
        string serialized,
        ArazzoOptions options,
        string documentName,
        ArazzoDocumentFormat format,
        IServiceProvider applicationServices,
        CancellationToken cancellationToken)
    {
        if (options.SerializedDocumentTransformers.Count == 0)
        {
            return serialized;
        }

        var context = new ArazzoSerializedDocumentContext
        {
            DocumentName = documentName, Format = format, ApplicationServices = applicationServices
        };

        foreach (var transformer in options.SerializedDocumentTransformers)
        {
            // Each transformer sees the previous one's output, so registration order is application order.
            serialized = await transformer.TransformAsync(serialized, context, cancellationToken);
        }

        return serialized;
    }
}
