using System.Text.Json.Nodes;

namespace Bielu.Overlay.Models;

/// <summary>An object that carries Overlay Specification Extensions — patterned fields prefixed <c>x-</c> (spec §4.6).</summary>
public interface IOverlayExtensible
{
    /// <summary>
    /// Properties that are not fixed fields of this object, or <see langword="null"/> when there are none.
    /// </summary>
    /// <remarks>
    /// Normally these are the <c>x-</c>-prefixed extensions of spec §4.6, but readers are deliberately
    /// tolerant: every unrecognized field is captured here, whatever its name, so nothing is silently
    /// dropped from a document that is round-tripped. With
    /// <c>OverlayReaderSettings.IgnoreUnrecognizedFields</c> disabled, a field that is neither a fixed
    /// field nor <c>x-</c>-prefixed also produces a warning diagnostic.
    /// </remarks>
    IDictionary<string, JsonNode?>? Extensions { get; set; }
}
