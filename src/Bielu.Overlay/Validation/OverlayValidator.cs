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
public static class OverlayValidator
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
            diagnostics.Add(new OverlayDiagnostic("/overlay",
                $"Unrecognized Overlay version '{overlay.Overlay}'; expected 1.0.x or 1.1.x.", IsWarning: true));
            version = OverlayVersion.V1_1;
        }

        ValidateInfo(overlay.Info, diagnostics);

        if (overlay.Actions.Count == 0)
        {
            diagnostics.Add(new OverlayDiagnostic("/actions", "The actions array MUST contain at least one value."));
        }

        for (var i = 0; i < overlay.Actions.Count; i++)
        {
            ValidateAction(overlay.Actions[i], $"/actions/{i}", version, diagnostics);
        }

        return diagnostics;
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
                "'remove' is set, so 'copy'/'update' on this action have no effect.", IsWarning: true));
        }
        else if (action.Copy is not null && action.Update is not null)
        {
            diagnostics.Add(new OverlayDiagnostic(path,
                "'copy' is set, so 'update' on this action has no effect.", IsWarning: true));
        }

        if (!action.Remove && action.Copy is null && action.Update is null)
        {
            diagnostics.Add(new OverlayDiagnostic(path,
                "Action has no effect: none of 'update', 'copy', or 'remove' is set.", IsWarning: true));
        }
    }
}
