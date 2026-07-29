using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Bielu.Overlay.Models;
using Json.Path;

namespace Bielu.Overlay.Validation;

/// <summary>
/// Structural checks an Overlay document must satisfy beyond what the type system already enforces:
/// a recognized version, non-empty required strings, at least one action, and targets that actually parse
/// as RFC 9535 JSONPath.
/// </summary>
/// <remarks>
/// Validation is deliberately separate from applying. <see cref="OverlayApplier"/> reports what went wrong
/// against a particular document; this reports what is wrong with the overlay on its own terms, which is
/// what a <c>validate</c> command needs when no target document is in hand.
/// </remarks>
public static partial class OverlayValidator
{
    /// <summary>Validates the structural invariants of an Overlay document.</summary>
    /// <param name="overlay">The overlay to validate.</param>
    /// <returns>The errors and warnings found.</returns>
    public static IReadOnlyList<OverlayDiagnostic> Validate(OverlayDocument overlay)
    {
        ArgumentNullException.ThrowIfNull(overlay);

        var diagnostics = new List<OverlayDiagnostic>();

        if (!OverlayVersionExtensions.TryParse(overlay.Overlay, out var version))
        {
            // Two different failures wear the same clothes here, and the specification treats them
            // differently. A version that is not even shaped like one ('1.1', 'abc') can never be a legal
            // Overlay document — the schema pins `overlay` to `^1\.\d+\.\d+$` — so that is an error. A
            // well-formed version we simply do not implement yet ('1.2.0', '2.0.0') is a document this
            // library cannot vouch for rather than one that is malformed, so that stays a warning and
            // application proceeds under the newest semantics we know.
            var malformed = !VersionShape().IsMatch(overlay.Overlay ?? string.Empty);
            diagnostics.Add(new OverlayDiagnostic("/overlay",
                malformed
                    ? $"'{overlay.Overlay}' is not a valid Overlay version; expected a 'MAJOR.MINOR.PATCH' string such as 1.0.0 or 1.1.0."
                    : $"Unrecognized Overlay version '{overlay.Overlay}'; expected 1.0.x or 1.1.x.",
                isWarning: !malformed));
            version = OverlayVersion.V1_1;
        }

        ValidateInfo(overlay.Info, diagnostics);
        ValidateExtensionNames(overlay.Extensions, "/", diagnostics);

        if (overlay.Actions.Count == 0)
        {
            diagnostics.Add(new OverlayDiagnostic("/actions", "The actions array MUST contain at least one value."));
        }

        for (var i = 0; i < overlay.Actions.Count; i++)
        {
            ValidateAction(overlay.Actions[i], $"/actions/{i}", version, diagnostics);
        }

        ValidateActionsAreUnique(overlay.Actions, diagnostics);

        return diagnostics;
    }

    [GeneratedRegex(@"^\d+\.\d+\.\d+$")]
    private static partial Regex VersionShape();

    /// <summary>
    /// The specification declares <c>actions</c> as <c>uniqueItems</c>, so two identical actions make the
    /// document invalid. Reported against the later of the pair, which is the one to delete.
    /// </summary>
    private static void ValidateActionsAreUnique(IList<OverlayAction> actions, List<OverlayDiagnostic> diagnostics)
    {
        for (var i = 1; i < actions.Count; i++)
        {
            for (var j = 0; j < i; j++)
            {
                if (AreEquivalent(actions[i], actions[j]))
                {
                    diagnostics.Add(new OverlayDiagnostic($"/actions/{i}",
                        $"Duplicate action: identical to /actions/{j}. The actions array MUST contain unique items."));
                    break;
                }
            }
        }
    }

    private static bool AreEquivalent(OverlayAction left, OverlayAction right) =>
        string.Equals(left.Target, right.Target, StringComparison.Ordinal)
        && string.Equals(left.Copy, right.Copy, StringComparison.Ordinal)
        && string.Equals(left.Description, right.Description, StringComparison.Ordinal)
        && left.Remove == right.Remove
        && JsonNode.DeepEquals(left.Update, right.Update);

    /// <summary>
    /// Fixed fields aside, the specification permits only <c>x-</c>-prefixed members (§4.6), and its schema
    /// closes every object with <c>unevaluatedProperties: false</c>. The reader keeps unknown members
    /// instead of discarding them — it is deliberately forgiving — so rejecting them is this validator's job.
    /// </summary>
    private static void ValidateExtensionNames(IDictionary<string, JsonNode?>? extensions, string path,
        List<OverlayDiagnostic> diagnostics)
    {
        if (extensions is null)
        {
            return;
        }

        foreach (var key in extensions.Keys)
        {
            if (!key.StartsWith("x-", StringComparison.Ordinal))
            {
                diagnostics.Add(new OverlayDiagnostic($"{path.TrimEnd('/')}/{key}",
                    $"'{key}' is not a known field. Only fixed fields and 'x-' prefixed extensions are permitted."));
            }
        }
    }

    private static void ValidateInfo(OverlayInfo info, List<OverlayDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(info.Title))
        {
            diagnostics.Add(new OverlayDiagnostic("/info/title", "info.title is required and MUST NOT be empty."));
        }

        if (string.IsNullOrWhiteSpace(info.Version))
        {
            diagnostics.Add(new OverlayDiagnostic("/info/version", "info.version is required and MUST NOT be empty."));
        }

        ValidateExtensionNames(info.Extensions, "/info", diagnostics);
    }

    private static void ValidateAction(OverlayAction action, string path, OverlayVersion version,
        List<OverlayDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(action.Target))
        {
            diagnostics.Add(new OverlayDiagnostic($"{path}/target", "action.target is required and MUST NOT be empty."));
        }
        else if (!JsonPath.TryParse(action.Target, out _))
        {
            diagnostics.Add(new OverlayDiagnostic($"{path}/target",
                $"'{action.Target}' is not a valid RFC 9535 JSONPath expression."));
        }

        if (action.Copy is not null)
        {
            if (version == OverlayVersion.V1_0)
            {
                diagnostics.Add(new OverlayDiagnostic($"{path}/copy",
                    "'copy' was introduced in Overlay 1.1.0; this document declares 1.0.0."));
            }

            if (string.IsNullOrWhiteSpace(action.Copy))
            {
                diagnostics.Add(new OverlayDiagnostic($"{path}/copy", "action.copy MUST NOT be empty."));
            }
            else if (!JsonPath.TryParse(action.Copy, out _))
            {
                diagnostics.Add(new OverlayDiagnostic($"{path}/copy",
                    $"'{action.Copy}' is not a valid RFC 9535 JSONPath expression."));
            }
        }

        // These fields are not mutually exclusive in the specification — `update` simply "has no impact"
        // when outranked — so an overlap is dead weight to report, not a malformed document to reject.
        if (action.Remove && (action.Copy is not null || action.Update is not null))
        {
            diagnostics.Add(new OverlayDiagnostic(path,
                "'remove' is set, so 'copy'/'update' on this action have no effect.", isWarning: true));
        }
        else if (action.Copy is not null && action.Update is not null)
        {
            diagnostics.Add(new OverlayDiagnostic(path,
                "'copy' is set, so 'update' on this action has no effect.", isWarning: true));
        }

        if (!action.Remove && action.Copy is null && action.Update is null)
        {
            diagnostics.Add(new OverlayDiagnostic(path,
                "Action has no effect: none of 'update', 'copy', or 'remove' is set.", isWarning: true));
        }

        ValidateExtensionNames(action.Extensions, path, diagnostics);
    }
}
