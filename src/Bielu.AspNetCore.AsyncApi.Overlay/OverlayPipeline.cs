// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Nodes;
using Bielu.Overlay;
using Bielu.Spec.Shared;
using Microsoft.Extensions.Logging;

namespace Bielu.AspNetCore.Overlay;

/// <summary>
/// An ordered set of overlays applied to a serialized API description at the point it is produced.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately spec-neutral: it takes a JSON or YAML string and returns one, so the same pipeline serves
/// the AsyncAPI and Arazzo integrations. The overlay engine underneath works on a
/// <see cref="JsonNode"/> tree and knows nothing about either specification.
/// </para>
/// <para>
/// The description is parsed once, every overlay is applied to the resulting tree in registration order,
/// and the tree is serialized once — so N overlays cost one round trip, not N.
/// </para>
/// </remarks>
public sealed class OverlayPipeline
{
    private static readonly JsonSerializerOptions JsonOutput = new() { WriteIndented = true };

    private readonly List<OverlaySource> _sources = [];

    /// <summary>Options controlling how each overlay is applied. Shared by every overlay in the pipeline.</summary>
    public OverlayApplyOptions ApplyOptions { get; } = new();

    /// <summary>Whether any overlay has been registered.</summary>
    public bool IsEmpty => _sources.Count == 0;

    /// <summary>Appends an overlay. Overlays apply in the order they are added.</summary>
    /// <param name="source">The overlay to append.</param>
    public void Add(OverlaySource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _sources.Add(source);
    }

    /// <summary>
    /// Applies every registered overlay to <paramref name="serialized"/> and returns the transformed
    /// description in the same format.
    /// </summary>
    /// <param name="serialized">The serialized description to transform.</param>
    /// <param name="format">The format <paramref name="serialized"/> is written in.</param>
    /// <param name="documentName">The name of the description being transformed, used in error messages.</param>
    /// <param name="logger">An optional logger for non-fatal diagnostics.</param>
    /// <returns>The transformed description.</returns>
    /// <exception cref="OverlayApplicationException">An overlay could not be loaded, the description could not be parsed, or applying an overlay reported an error.</exception>
    public string Apply(string serialized, OverlayDocumentFormat format, string documentName, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(serialized);

        if (_sources.Count == 0)
        {
            return serialized;
        }

        var node = Parse(serialized, format, documentName);

        foreach (var source in _sources)
        {
            var overlay = source.Resolve();
            var result = OverlayApplier.Apply(node, overlay, ApplyOptions);

            foreach (var diagnostic in result.Diagnostics.Where(d => d.IsWarning))
            {
                // Every one of these reaches us from outside the process — the overlay's own file
                // contents in the case of the diagnostic, a configured path in the case of the origin —
                // so newlines are stripped before they reach the log to stop a crafted value forging
                // extra log entries (CWE-117).
                logger?.LogWarning("Overlay '{Overlay}' on document '{DocumentName}' reported {Path}: {Message}",
                    SanitizeLog(source.Origin), SanitizeLog(documentName),
                    SanitizeLog(diagnostic.Path), SanitizeLog(diagnostic.Message));
            }

            if (result.HasErrors)
            {
                var detail = string.Join("; ",
                    result.Diagnostics.Where(d => !d.IsWarning).Select(d => $"{d.Path}: {d.Message}"));
                throw new OverlayApplicationException(
                    $"Applying overlay '{source.Origin}' to document '{documentName}' failed: {detail}");
            }

            node = result.Document;
        }

        return format == OverlayDocumentFormat.Yaml
            ? JsonNodeToYamlConverter.Serialize(node)
            : node?.ToJsonString(JsonOutput) ?? string.Empty;
    }

    /// <summary>
    /// Removes newline characters so a value cannot forge additional log entries (CWE-117). Mirrors the
    /// helper the AsyncAPI and Arazzo endpoint extensions already apply to their own logged values.
    /// </summary>
    private static string SanitizeLog(string? value) =>
        value is null ? string.Empty : value.Replace("\r", "").Replace("\n", "");

    private static JsonNode? Parse(string serialized, OverlayDocumentFormat format, string documentName)
    {
        try
        {
            return format == OverlayDocumentFormat.Yaml
                ? YamlToJsonNodeConverter.Convert(new StringReader(serialized))
                : JsonNode.Parse(serialized);
        }
        // Intentionally broad: the YAML path surfaces YamlDotNet's own exception types, which this
        // assembly does not reference directly, and every failure here means the same thing — the
        // description we were handed is not parseable in the format it claims to be.
        catch (Exception ex)
        {
            throw new OverlayApplicationException(
                $"Document '{documentName}' could not be parsed as {format} before applying overlays: {ex.Message}", ex);
        }
    }
}
