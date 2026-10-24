using System.Text.Json.Serialization;

namespace Bielu.AspNetCore.AsyncApi.XRegistry.Internal;

/// <summary>The host-based discovery document served at <c>/.well-known/xregistry</c>.</summary>
/// <param name="Registries">The absolute URLs of the registries on this host.</param>
internal sealed record XRegistryDiscovery([property: JsonPropertyName("registries")] IReadOnlyList<string> Registries);

[JsonSerializable(typeof(XRegistryDiscovery))]
internal sealed partial class XRegistryJsonContext : JsonSerializerContext;
