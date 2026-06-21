using ByteBard.AsyncAPI.Bindings;
using ByteBard.AsyncAPI.Readers.ParseNodes;
using ByteBard.AsyncAPI.Writers;

namespace Bielu.AspNetCore.AsyncApi.Extensions.Protocols.Sse;

/// <summary>
/// Server binding for the <c>sse</c> protocol. Describes capabilities advertised by an SSE endpoint
/// such as its default reconnection time and whether it emits heartbeat comments.
/// </summary>
public class SseServerBinding : ServerBinding<SseServerBinding>
{
    /// <summary>The default reconnection time in milliseconds the server advertises to clients.</summary>
    public int? Retry { get; set; }

    /// <summary>Whether the server periodically emits comment lines (<c>:</c>) to keep the stream alive.</summary>
    public bool? Heartbeat { get; set; }

    /// <inheritdoc />
    public override string BindingKey => SseProtocol.ProtocolName;

    /// <inheritdoc />
    protected override FixedFieldMap<SseServerBinding> FixedFieldMap => new()
    {
        { "bindingVersion", (a, n) => a.BindingVersion = n.GetScalarValue() },
        { "retry", (a, n) => a.Retry = ParseRetry(n.GetScalarValue()) },
        { "heartbeat", (a, n) => a.Heartbeat = n.GetBooleanValue() },
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
        writer.WriteOptionalProperty("retry", Retry);
        writer.WriteOptionalProperty("heartbeat", Heartbeat);
        writer.WriteOptionalProperty("bindingVersion", BindingVersion ?? SseProtocol.DefaultBindingVersion);
        writer.WriteExtensions(Extensions);
        writer.WriteEndObject();
    }

    private static int? ParseRetry(string? value) =>
        int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
}
