// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Bielu.AspNetCore.Arazzo.Services;
using Bielu.AspNetCore.Overlay;
using Bielu.Overlay;
using Bielu.Overlay.Models;

namespace Bielu.AspNetCore.Arazzo.Overlay;

/// <summary>
/// Registers OpenAPI Overlays on an <see cref="ArazzoOptions"/>, applied at the serialization boundary so
/// the document served by <c>MapArazzo()</c> is already transformed.
/// </summary>
/// <remarks>
/// The Overlay Specification is written against OpenAPI descriptions; applying overlays to Arazzo is an
/// extension this library offers, not a conformance claim. See <c>Bielu.Overlay.OverlayApplier</c> for the
/// specification-status detail.
/// </remarks>
/// <example>
/// <code>
/// builder.Services.AddArazzo("workflows", options =>
/// {
///     options.AddOverlay("overlays/public-workflows.yaml");
/// });
/// </code>
/// </example>
public static class ArazzoOverlayOptionsExtensions
{
    // Same reasoning as the AsyncAPI side: ArazzoOptions has no extensibility bag, and instances are
    // rebuilt whenever IOptionsMonitor reloads, so the pipeline is attached per instance and collected
    // with it.
    private static readonly ConditionalWeakTable<ArazzoOptions, ArazzoOverlayTransformer> Transformers = new();

    /// <summary>Adds an overlay read from a JSON or YAML file.</summary>
    /// <param name="options">The options to register the overlay on.</param>
    /// <param name="filePath">Path to the overlay file. Relative paths resolve against the process working directory. The file is read once, on first use.</param>
    /// <returns>The <see cref="ArazzoOptions"/> instance for further customization.</returns>
    public static ArazzoOptions AddOverlay(this ArazzoOptions options, string filePath)
        => options.AddOverlay(OverlaySource.FromFile(filePath));

    /// <summary>Adds an already-constructed overlay document.</summary>
    /// <param name="options">The options to register the overlay on.</param>
    /// <param name="overlay">The overlay to apply.</param>
    /// <returns>The <see cref="ArazzoOptions"/> instance for further customization.</returns>
    public static ArazzoOptions AddOverlay(this ArazzoOptions options, OverlayDocument overlay)
        => options.AddOverlay(OverlaySource.FromDocument(overlay));

    /// <summary>Adds an overlay from an arbitrary source. Overlays apply in the order they are added.</summary>
    /// <param name="options">The options to register the overlay on.</param>
    /// <param name="source">The overlay source to apply.</param>
    /// <returns>The <see cref="ArazzoOptions"/> instance for further customization.</returns>
    public static ArazzoOptions AddOverlay(this ArazzoOptions options, OverlaySource source)
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
    /// <returns>The <see cref="ArazzoOptions"/> instance for further customization.</returns>
    public static ArazzoOptions ConfigureOverlays(this ArazzoOptions options, Action<OverlayApplyOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(configure);

        configure(GetOrCreateTransformer(options).Pipeline.ApplyOptions);
        return options;
    }

    private static ArazzoOverlayTransformer GetOrCreateTransformer(ArazzoOptions options)
    {
        if (Transformers.TryGetValue(options, out var existing))
        {
            return existing;
        }

        var created = new ArazzoOverlayTransformer(new OverlayPipeline());
        Transformers.Add(options, created);
        options.AddSerializedDocumentTransformer(created);
        return created;
    }
}
