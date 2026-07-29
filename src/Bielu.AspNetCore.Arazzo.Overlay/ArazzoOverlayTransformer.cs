// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bielu.AspNetCore.Arazzo.Transformers;
using Bielu.AspNetCore.Overlay;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bielu.AspNetCore.Arazzo.Overlay;

/// <summary>Adapts an <see cref="OverlayPipeline"/> onto the Arazzo package's serialized-document seam.</summary>
internal sealed class ArazzoOverlayTransformer(OverlayPipeline pipeline) : IArazzoSerializedDocumentTransformer
{
    public OverlayPipeline Pipeline { get; } = pipeline;

    public ValueTask<string> TransformAsync(string document, ArazzoSerializedDocumentContext context,
        CancellationToken cancellationToken)
    {
        var logger = context.ApplicationServices.GetService<ILoggerFactory>()
            ?.CreateLogger("Bielu.AspNetCore.Arazzo.Overlay");

        var format = context.Format == ArazzoDocumentFormat.Yaml
            ? OverlayDocumentFormat.Yaml
            : OverlayDocumentFormat.Json;

        // CPU-bound tree work over an in-memory document — nothing to await.
        return ValueTask.FromResult(Pipeline.Apply(document, format, context.DocumentName, logger));
    }
}
