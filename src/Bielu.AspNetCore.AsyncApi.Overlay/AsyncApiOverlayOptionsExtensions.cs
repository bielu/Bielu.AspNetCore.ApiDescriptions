// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Bielu.AspNetCore.AsyncApi.Services;
using Bielu.AspNetCore.Overlay;
using Bielu.Overlay;
using Bielu.Overlay.Models;

namespace Bielu.AspNetCore.AsyncApi.Overlay;

/// <summary>
/// Registers OpenAPI Overlays on an <see cref="AsyncApiOptions"/>, applied at the serialization boundary
/// so the document served by <c>MapAsyncApi()</c> is already transformed.
/// </summary>
/// <example>
/// <code>
/// builder.Services.AddAsyncApi("v1", options =>
/// {
///     options.AddOverlay("overlays/public.yaml");
/// });
/// </code>
/// </example>
public static class AsyncApiOverlayOptionsExtensions
{
    // AsyncApiOptions has no extensibility bag, and its transformer list is internal to the core package,
    // so the pipeline is attached to the options instance from out here. A ConditionalWeakTable is the
    // right shape: options instances are rebuilt whenever IOptionsMonitor reloads, and each rebuilt
    // instance re-runs the configure callbacks and so gets its own pipeline, with the old one collected.
    private static readonly ConditionalWeakTable<AsyncApiOptions, AsyncApiOverlayTransformer> Transformers = new();

    /// <summary>Adds an overlay read from a JSON or YAML file.</summary>
    /// <param name="options">The options to register the overlay on.</param>
    /// <param name="filePath">Path to the overlay file. Relative paths resolve against the process working directory. The file is read once, on first use.</param>
    /// <returns>The <see cref="AsyncApiOptions"/> instance for further customization.</returns>
    public static AsyncApiOptions AddOverlay(this AsyncApiOptions options, string filePath)
        => options.AddOverlay(OverlaySource.FromFile(filePath));

    /// <summary>Adds an already-constructed overlay document.</summary>
    /// <param name="options">The options to register the overlay on.</param>
    /// <param name="overlay">The overlay to apply.</param>
    /// <returns>The <see cref="AsyncApiOptions"/> instance for further customization.</returns>
    public static AsyncApiOptions AddOverlay(this AsyncApiOptions options, OverlayDocument overlay)
        => options.AddOverlay(OverlaySource.FromDocument(overlay));

    /// <summary>Adds an overlay from an arbitrary source. Overlays apply in the order they are added.</summary>
    /// <param name="options">The options to register the overlay on.</param>
    /// <param name="source">The overlay source to apply.</param>
    /// <returns>The <see cref="AsyncApiOptions"/> instance for further customization.</returns>
    public static AsyncApiOptions AddOverlay(this AsyncApiOptions options, OverlaySource source)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(source);

        GetOrCreateTransformer(options).Pipeline.Add(source);
        return options;
    }

    /// <summary>
    /// Configures how this document's overlays are applied — notably <see cref="OverlayApplyOptions.Strict"/>,
    /// which turns a <c>target</c> matching zero nodes from a logged warning into a failure.
    /// </summary>
    /// <param name="options">The options whose overlay application to configure.</param>
    /// <param name="configure">A delegate configuring the shared <see cref="OverlayApplyOptions"/>.</param>
    /// <returns>The <see cref="AsyncApiOptions"/> instance for further customization.</returns>
    public static AsyncApiOptions ConfigureOverlays(this AsyncApiOptions options, Action<OverlayApplyOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(configure);

        configure(GetOrCreateTransformer(options).Pipeline.ApplyOptions);
        return options;
    }

    private static AsyncApiOverlayTransformer GetOrCreateTransformer(AsyncApiOptions options)
    {
        if (Transformers.TryGetValue(options, out var existing))
        {
            return existing;
        }

        // One transformer per options instance, registered on first use, so every overlay shares a single
        // parse/serialize round trip and applies in declaration order regardless of how many calls added them.
        var created = new AsyncApiOverlayTransformer(new OverlayPipeline());
        Transformers.Add(options, created);
        options.AddSerializedDocumentTransformer(created);
        return created;
    }
}
