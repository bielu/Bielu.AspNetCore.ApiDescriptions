using Bielu.Arazzo.Writers;

namespace Bielu.Arazzo.Models;

/// <summary>
/// Implemented by every model type so <see cref="ArazzoJsonWriter"/> and <see cref="ArazzoYamlWriter"/>
/// share one traversal. Named per-version (mirroring how ByteBard.AsyncAPI.NET exposes
/// <c>SerializeV2</c>/<c>SerializeV3</c>) even though only 1.x exists today, so a 2.0 can be added without
/// a breaking rename.
/// </summary>
public interface IArazzoSerializable
{
    void SerializeAsV1(IArazzoWriter writer);
}
