using ByteBard.AsyncAPI.Bindings;
using ByteBard.AsyncAPI.Readers.ParseNodes;
using ByteBard.AsyncAPI.Writers;

namespace Bielu.AspNetCore.AsyncApi.Extensions.Protocols.SignalR;

/// <summary>
/// Operation binding for the <c>signalr</c> protocol. An operation maps to a single hub method
/// (client-to-server invocation) or a server-to-client push.
/// </summary>
public class SignalROperationBinding : OperationBinding<SignalROperationBinding>
{
    /// <summary>The hub method name being invoked or pushed.</summary>
    public string? Target { get; set; }

    /// <summary>Direction of the call (see <see cref="SignalRProtocol.Directions"/>).</summary>
    public string? Direction { get; set; }

    /// <summary>The kind of SignalR call (see <see cref="SignalRProtocol.CallTypes"/>).</summary>
    public string? CallType { get; set; }

    /// <summary>Whether the method streams its result back to the caller.</summary>
    public bool? Streaming { get; set; }

    /// <inheritdoc />
    public override string BindingKey => SignalRProtocol.ProtocolName;

    /// <inheritdoc />
    protected override FixedFieldMap<SignalROperationBinding> FixedFieldMap => new()
    {
        { "bindingVersion", (a, n) => a.BindingVersion = n.GetScalarValue() },
        { "target", (a, n) => a.Target = n.GetScalarValue() },
        { "direction", (a, n) => a.Direction = n.GetScalarValue() },
        { "callType", (a, n) => a.CallType = n.GetScalarValue() },
        { "streaming", (a, n) => a.Streaming = n.GetBooleanValue() },
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
        writer.WriteOptionalProperty("target", Target);
        writer.WriteOptionalProperty("direction", Direction);
        writer.WriteOptionalProperty("callType", CallType);
        writer.WriteOptionalProperty("streaming", Streaming);
        writer.WriteOptionalProperty("bindingVersion", BindingVersion ?? SignalRProtocol.DefaultBindingVersion);
        writer.WriteExtensions(Extensions);
        writer.WriteEndObject();
    }
}
