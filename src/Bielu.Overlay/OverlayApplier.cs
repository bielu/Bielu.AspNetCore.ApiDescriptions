using System.Text.Json.Nodes;
using Bielu.Overlay.Models;
using Json.Path;

namespace Bielu.Overlay;

/// <summary>
/// Applies an <see cref="OverlayDocument"/> to a document represented as a <see cref="JsonNode"/> tree.
/// </summary>
/// <remarks>
/// <para>
/// The engine is deliberately spec-agnostic: it sees a <see cref="JsonNode"/> and nothing else, so the
/// same overlay machinery applies to OpenAPI, AsyncAPI, Arazzo, or any other JSON/YAML description. The
/// Overlay Specification is written against OpenAPI, but its mechanism — select by JSONPath, then
/// merge/copy/remove — carries no OpenAPI-specific assumptions.
/// </para>
/// <para>
/// <b>Targeting anything other than OpenAPI is not sanctioned by the specification today.</b> The OAI
/// closed <see href="https://github.com/OAI/Overlay-Specification/issues/268">Overlay-Specification#268</see>
/// as <c>not planned</c> in February 2026, on the grounds that this "is not a 'core function' of this
/// specification, which is intended for OpenAPI descriptions". The question is being revisited in
/// <see href="https://github.com/OAI/Overlay-Specification/issues/367">#367</see> with a draft PR, but
/// the working group remains broadly aligned with the original decision, so treat AsyncAPI and Arazzo
/// targeting as an extension this library offers rather than a conformance claim. Behaviour against
/// OpenAPI documents stays spec-exact either way, so overlays remain portable to other tooling.
/// </para>
/// <para>
/// Actions are applied <b>in sequence, each against the result of the last</b> (spec §4.4.1), which is
/// what lets an overlay delete a node in one action and re-create it in a later one. Application is
/// best-effort: an action that fails is reported and skipped, and the remaining actions still run.
/// </para>
/// </remarks>
public static class OverlayApplier
{
    /// <summary>Applies <paramref name="overlay"/> to <paramref name="document"/>, returning a transformed copy.</summary>
    /// <param name="document">The document to transform. Never mutated — the result is a distinct tree.</param>
    /// <param name="overlay">The overlay to apply.</param>
    /// <param name="options">Options controlling strictness; defaults are used when omitted.</param>
    /// <returns>The transformed document plus any diagnostics collected while applying.</returns>
    public static OverlayApplyResult Apply(JsonNode? document, OverlayDocument overlay, OverlayApplyOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(overlay);

        options ??= new OverlayApplyOptions();
        var diagnostics = new List<OverlayDiagnostic>();

        if (!OverlayVersionExtensions.TryParse(overlay.Overlay, out var version))
        {
            // Unknown version: warn and proceed with the newest semantics we know, rather than refusing to
            // apply an otherwise well-formed document.
            diagnostics.Add(new OverlayDiagnostic("/overlay",
                $"Unrecognized Overlay version '{overlay.Overlay}'; expected 1.0.x or 1.1.x. Applying 1.1.0 semantics.",
                IsWarning: true));
            version = OverlayVersion.V1_1;
        }

        // Work on a copy so callers can apply the same overlay to several documents, and so a partially
        // applied overlay never leaves the caller's tree half-transformed.
        var root = document?.DeepClone();

        if (root is null)
        {
            diagnostics.Add(new OverlayDiagnostic("/", "Target document is null; nothing to apply."));
            return new OverlayApplyResult { Document = null, Diagnostics = diagnostics };
        }

        for (var i = 0; i < overlay.Actions.Count; i++)
        {
            ApplyAction(root, overlay.Actions[i], $"/actions/{i}", version, options, diagnostics);
        }

        return new OverlayApplyResult { Document = root, Diagnostics = diagnostics };
    }

    private static void ApplyAction(JsonNode root, OverlayAction action, string path, OverlayVersion version,
        OverlayApplyOptions options, List<OverlayDiagnostic> diagnostics)
    {
        if (!JsonPath.TryParse(action.Target, out var target))
        {
            diagnostics.Add(new OverlayDiagnostic($"{path}/target",
                $"'{action.Target}' is not a valid RFC 9535 JSONPath expression."));
            return;
        }

        // Materialize before mutating: the match list must not be re-evaluated against a tree we are
        // in the middle of editing.
        var matches = target.Evaluate(root).Matches
            .Select(m => m.Value)
            .OfType<JsonNode>()
            .ToList();

        if (matches.Count == 0)
        {
            diagnostics.Add(new OverlayDiagnostic($"{path}/target",
                $"Target '{action.Target}' matched no nodes.", IsWarning: !options.Strict));
            return;
        }

        // Precedence per spec §4.4.3: `update` "has no impact if the `remove` field ... is `true` or if the
        // `copy` field contains a value". These are not mutually exclusive fields, so this ordering — not a
        // validation error — is what resolves a document that sets more than one.
        if (action.Remove)
        {
            ApplyRemove(matches, action, path, diagnostics);
            return;
        }

        if (action.Copy is not null)
        {
            ApplyCopy(root, matches, action, path, version, diagnostics);
            return;
        }

        if (action.Update is not null)
        {
            foreach (var match in matches)
            {
                ApplyValue(match, action.Update, path, version, diagnostics);
            }

            return;
        }

        diagnostics.Add(new OverlayDiagnostic(path,
            "Action has no effect: none of 'update', 'copy', or 'remove' is set.", IsWarning: true));
    }

    private static void ApplyRemove(List<JsonNode> matches, OverlayAction action, string path,
        List<OverlayDiagnostic> diagnostics)
    {
        foreach (var match in matches)
        {
            if (!JsonNodeEditor.TryRemove(match))
            {
                diagnostics.Add(new OverlayDiagnostic($"{path}/target",
                    $"Target '{action.Target}' selected a node that cannot be removed (it is the document root)."));
            }
        }
    }

    private static void ApplyCopy(JsonNode root, List<JsonNode> matches, OverlayAction action, string path,
        OverlayVersion version, List<OverlayDiagnostic> diagnostics)
    {
        if (version == OverlayVersion.V1_0)
        {
            diagnostics.Add(new OverlayDiagnostic($"{path}/copy",
                "'copy' requires Overlay 1.1.0; this document declares 1.0.0."));
            return;
        }

        if (!JsonPath.TryParse(action.Copy!, out var copyPath))
        {
            diagnostics.Add(new OverlayDiagnostic($"{path}/copy",
                $"'{action.Copy}' is not a valid RFC 9535 JSONPath expression."));
            return;
        }

        var sources = copyPath.Evaluate(root).Matches.Select(m => m.Value).OfType<JsonNode>().ToList();
        if (sources.Count != 1)
        {
            // The spec defines `copy` as "selecting a single node"; anything else is ambiguous.
            diagnostics.Add(new OverlayDiagnostic($"{path}/copy",
                $"'{action.Copy}' must select exactly one node, but selected {sources.Count}."));
            return;
        }

        // Snapshot the source before editing: a copy whose target overlaps its source would otherwise
        // observe its own partial writes.
        var source = sources[0].DeepClone();

        foreach (var match in matches)
        {
            ApplyValue(match, source, path, version, diagnostics);
        }
    }

    /// <summary>Applies a value to one selected node, dispatching on the node's kind per spec §4.4.3.</summary>
    private static void ApplyValue(JsonNode match, JsonNode value, string path, OverlayVersion version,
        List<OverlayDiagnostic> diagnostics)
    {
        switch (match)
        {
            case JsonObject targetObject when value is JsonObject valueObject:
                MergeObject(targetObject, valueObject);
                return;

            case JsonObject:
                diagnostics.Add(new OverlayDiagnostic(path,
                    "Target selects an object node, so the applied value must be an object."));
                return;

            case JsonArray targetArray:
                AppendToArray(targetArray, value, version);
                return;

            default:
                // A primitive (or null) node. Replacing one in place is 1.1.0-only; 1.0.0 required the
                // target to select the *containing* object instead.
                if (version == OverlayVersion.V1_0)
                {
                    diagnostics.Add(new OverlayDiagnostic(path,
                        "Target selects a primitive node, which Overlay 1.0.0 does not permit. Target the containing object instead, or declare 'overlay: 1.1.0'."));
                    return;
                }

                if (!JsonNodeEditor.TryReplace(match, value.DeepClone()))
                {
                    diagnostics.Add(new OverlayDiagnostic(path,
                        "Target selects a primitive node that cannot be replaced (it is the document root)."));
                }

                return;
        }
    }

    private static void AppendToArray(JsonArray target, JsonNode value, OverlayVersion version)
    {
        // 1.1.0: an array value concatenates element-wise; anything else appends as one entry.
        // 1.0.0: the value is always "an entry to append", so an array appends as a single nested entry.
        if (version == OverlayVersion.V1_1 && value is JsonArray valueArray)
        {
            foreach (var item in valueArray)
            {
                target.Add(item?.DeepClone());
            }

            return;
        }

        target.Add(value.DeepClone());
    }

    /// <summary>
    /// Recursively merges <paramref name="update"/> into <paramref name="target"/>: matching object-valued
    /// properties merge, everything else replaces.
    /// </summary>
    /// <remarks>
    /// The specification says update properties are "recursively merged" but never states what a nested
    /// <em>array</em> does. We replace it. The concatenate/append rule is defined for the case where
    /// <c>target</c> itself selects an array node, and extending it to arrays encountered mid-merge would
    /// make it impossible to overwrite an array at all.
    /// </remarks>
    private static void MergeObject(JsonObject target, JsonObject update)
    {
        foreach (var (key, value) in update)
        {
            if (target[key] is JsonObject existing && value is JsonObject nested)
            {
                MergeObject(existing, nested);
                continue;
            }

            // DeepClone: a JsonNode belongs to exactly one parent, and the overlay's own tree must stay
            // intact so the same overlay can be applied again to another document.
            target[key] = value?.DeepClone();
        }
    }
}
