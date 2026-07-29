// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bielu.AspNetCore.AsyncApi.Transformers;
using Bielu.AspNetCore.Overlay;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bielu.AspNetCore.AsyncApi.Overlay;

/// <summary>
/// Adapts an <see cref="OverlayPipeline"/> onto the core package's serialized-document seam.
/// </summary>
internal sealed class AsyncApiOverlayTransformer(OverlayPipeline pipeline) : IAsyncApiSerializedDocumentTransformer
{
    public OverlayPipeline Pipeline { get; } = pipeline;

    public ValueTask<string> TransformAsync(string document, AsyncApiSerializedDocumentContext context, CancellationToken cancellationToken)
    {
        var logger = context.ApplicationServices.GetService<ILoggerFactory>()
            ?.CreateLogger("Bielu.AspNetCore.AsyncApi.Overlay");

        var format = context.Format == AsyncApiDocumentFormat.Yaml
            ? OverlayDocumentFormat.Yaml
            : OverlayDocumentFormat.Json;

        // Overlay application is CPU-bound tree work over an in-memory document, so there is nothing to
        // await; the seam is async because other transformers may genuinely need it.
        return ValueTask.FromResult(Pipeline.Apply(document, format, context.DocumentName, logger));
    }
}
