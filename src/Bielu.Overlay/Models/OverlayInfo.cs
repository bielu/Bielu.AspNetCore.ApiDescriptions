using System.Text.Json.Nodes;

namespace Bielu.Overlay.Models;

/// <summary>Spec §4.4.2 Info Object — metadata about the Overlay.</summary>
public sealed class OverlayInfo : IOverlayExtensible
{
    /// <summary>A human readable description of the purpose of the overlay.</summary>
    public required string Title { get; set; }

    /// <summary>A version identifier for indicating changes to the Overlay document. Distinct from the Overlay Specification version in <see cref="OverlayDocument.Overlay"/>.</summary>
    public required string Version { get; set; }

    /// <summary>A description of the Overlay document. CommonMark syntax MAY be used.</summary>
    public string? Description { get; set; }

    /// <inheritdoc />
    public IDictionary<string, JsonNode?>? Extensions { get; set; }
}
