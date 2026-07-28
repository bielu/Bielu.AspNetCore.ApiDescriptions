using System.Text.Json.Nodes;

namespace Bielu.Overlay.Models;

/// <summary>Spec §4.4.1 Overlay Object — the root of an Overlay document.</summary>
public sealed class OverlayDocument : IOverlayExtensible
{
    /// <summary>The Overlay Specification version this document uses, e.g. "1.1.0". Parse with <see cref="OverlayVersionExtensions.TryParse"/>.</summary>
    public required string Overlay { get; set; }

    /// <summary>Metadata about the Overlay.</summary>
    public required OverlayInfo Info { get; set; }

    /// <summary>
    /// A URI reference identifying the target document this overlay was designed for. Advisory only:
    /// where it is absent, selecting a target document is the tooling's responsibility.
    /// </summary>
    /// <remarks>
    /// This is never dereferenced. Resolving it over the network from inside a hosted application would
    /// let an overlay file drive outbound requests; it is used, at most, to verify that the document
    /// being overlaid is the one the overlay expected.
    /// </remarks>
    public string? Extends { get; set; }

    /// <summary>
    /// The ordered list of actions to apply. MUST contain at least one entry, and MUST be applied in
    /// sequence — each action sees the result of the previous one, which is what lets a document delete
    /// a node in one action and re-create it in a later one.
    /// </summary>
    public required IList<OverlayAction> Actions { get; set; }

    /// <inheritdoc />
    public IDictionary<string, JsonNode?>? Extensions { get; set; }
}
