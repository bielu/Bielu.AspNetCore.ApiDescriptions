using System.Text.Json.Serialization;

namespace Bielu.AspNetCore.Scalar.Plugins;

/// <summary>Source-generated serialization of the options a plugin's window global holds, so the package stays AOT-compatible.</summary>
[JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class ScalarPluginOptionsJsonContext : JsonSerializerContext;
