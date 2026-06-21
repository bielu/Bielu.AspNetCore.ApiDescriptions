using ByteBard.AsyncAPI.Bindings;
using ByteBard.AsyncAPI.Readers.ParseNodes;
using ByteBard.AsyncAPI.Writers;

namespace Bielu.AspNetCore.AsyncApi.Extensions.Protocols.Sse;

/// <summary>
/// Operation binding for the <c>sse</c> protocol. An operation maps to subscribing to the event
/// stream; SSE is one-way, so operations are always server-to-client.
/// </summary>
public class SseOperationBinding : OperationBinding<SseOperationBinding>
{
    /// <summary>The HTTP method used to open the stream (see <see cref="SseProtocol.Methods"/>).</summary>
    public string? Method { get; set; }

    /// <summary>Direction of the stream (always <see cref="SseProtocol.Directions.ServerToClient"/>).</summary>
    public string? Direction { get; set; }

    /// <inheritdoc />
    public override string BindingKey => SseProtocol.ProtocolName;

    /// <inheritdoc />
    protected override FixedFieldMap<SseOperationBinding> FixedFieldMap => new()
    {
        { "bindingVersion", (a, n) => a.BindingVersion = n.GetScalarValue() },
        { "method", (a, n) => a.Method = n.GetScalarValue() },
        { "direction", (a, n) => a.Direction = n.GetScalarValue() },
    };

    /// <inheritdoc />
    public override void SerializeV2(IAsyncApiWriter writer) => Serialize(writer);

    /// <inheritdoc />
    public override void SerializeV3(IAsyncApiWriter writer) => Serialize(writer);

    /// <inheritdoc />
    public override void SerializeProperties(IAsyncApiWriter writer) => Serialize(writer);

    private void Serialize(IAsyncApiWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteStartObject();
        writer.WriteOptionalProperty("method", Method);
        writer.WriteOptionalProperty("direction", Direction ?? SseProtocol.Directions.ServerToClient);
        writer.WriteOptionalProperty("bindingVersion", BindingVersion ?? SseProtocol.DefaultBindingVersion);
        writer.WriteExtensions(Extensions);
        writer.WriteEndObject();
    }
}
