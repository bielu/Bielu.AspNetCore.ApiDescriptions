using System.Text.Json.Nodes;

namespace Bielu.Overlay.Models;

/// <summary>
/// Spec §4.4.3 Action Object — one or more changes applied at the locations selected by <see cref="Target"/>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Update"/>, <see cref="Copy"/>, and <see cref="Remove"/> are <b>not</b> mutually exclusive in
/// the specification, so a document setting more than one is legal rather than malformed. The spec resolves
/// the overlap by precedence: <c>update</c> "has no impact if the <c>remove</c> field of this action object
/// is <c>true</c> or if the <c>copy</c> field contains a value", giving
/// <see cref="Remove"/> &gt; <see cref="Copy"/> &gt; <see cref="Update"/>. The apply engine follows exactly
/// that order; the validator reports the redundancy as a warning, not an error.
/// </para>
/// <para>
/// A <c>null</c> <see cref="Update"/> is treated as absent. JSON <c>null</c> has no sanctioned meaning for
/// <c>update</c> — deleting is <see cref="Remove"/>'s job, not a null-merge as in JSON Merge Patch
/// (RFC 7386) — and the specification never demonstrates it.
/// </para>
/// </remarks>
public sealed class OverlayAction : IOverlayExtensible
{
    /// <summary>An RFC 9535 JSONPath query expression selecting nodes in the target document.</summary>
    public required string Target { get; set; }

    /// <summary>A description of the action. CommonMark syntax MAY be used.</summary>
    public string? Description { get; set; }

    /// <summary>
    /// The value to merge into each selected node: an object to merge into object nodes, an array to
    /// concatenate with — or an object/primitive to append to — array nodes, or a primitive replacing
    /// primitive nodes. Array concatenation and primitive targets require <see cref="OverlayVersion.V1_1"/>.
    /// </summary>
    public JsonNode? Update { get; set; }

    /// <summary>
    /// A JSONPath expression selecting a single node in the target document to copy into the selected
    /// nodes. New in <see cref="OverlayVersion.V1_1"/>; sequenced with <see cref="Update"/> or
    /// <see cref="Remove"/> actions it expresses moves and renames.
    /// </summary>
    public string? Copy { get; set; }

    /// <summary>Whether the selected nodes MUST be removed from the map or array containing them. Defaults to <see langword="false"/>.</summary>
    public bool Remove { get; set; }

    /// <inheritdoc />
    public IDictionary<string, JsonNode?>? Extensions { get; set; }
}
