using System.Text.Json.Nodes;

namespace Bielu.Overlay.Models;

/// <summary>An object that carries Overlay Specification Extensions — patterned fields prefixed <c>x-</c> (spec §4.6).</summary>
public interface IOverlayExtensible
{
    /// <summary>A dictionary of <c>x-</c>-prefixed extension properties, or <see langword="null"/> when the object declares none.</summary>
    IDictionary<string, JsonNode?>? Extensions { get; set; }
}
