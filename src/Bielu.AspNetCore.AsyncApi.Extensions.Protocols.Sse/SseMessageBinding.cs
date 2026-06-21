using ByteBard.AsyncAPI.Bindings;
using ByteBard.AsyncAPI.Models;
using ByteBard.AsyncAPI.Readers;
using ByteBard.AsyncAPI.Readers.ParseNodes;
using ByteBard.AsyncAPI.Writers;

namespace Bielu.AspNetCore.AsyncApi.Extensions.Protocols.Sse;

/// <summary>
/// Message binding for the <c>sse</c> protocol. Describes how a single event is framed on the wire
/// using the <c>event</c>/<c>id</c>/<c>retry</c>/<c>data</c> fields of an <c>text/event-stream</c>.
/// </summary>
public class SseMessageBinding : MessageBinding<SseMessageBinding>
{
    /// <summary>The value of the <c>event:</c> field (the named event type clients listen for).</summary>
    public string? Event { get; set; }

    /// <summary>An example or pattern for the <c>id:</c> field carried with the event.</summary>
    public string? Id { get; set; }

    /// <summary>The reconnection time in milliseconds advertised via the <c>retry:</c> field.</summary>
    public int? Retry { get; set; }

    /// <summary>Schema describing the structure of the event <c>data:</c> payload, when not the message payload.</summary>
    public AsyncApiJsonSchema? Headers { get; set; }

    /// <inheritdoc />
    public override string BindingKey => SseProtocol.ProtocolName;

    /// <inheritdoc />
    protected override FixedFieldMap<SseMessageBinding> FixedFieldMap => new()
    {
        { "bindingVersion", (a, n) => a.BindingVersion = n.GetScalarValue() },
        { "event", (a, n) => a.Event = n.GetScalarValue() },
        { "id", (a, n) => a.Id = n.GetScalarValue() },
        { "retry", (a, n) => a.Retry = ParseRetry(n.GetScalarValue()) },
        { "headers", (a, n) => a.Headers = AsyncApiJsonSchemaDeserializer.LoadSchema(n) },
    };

    /// <inheritdoc />
    public override void SerializeV2(IAsyncApiWriter writer) => Serialize(writer, useV2Schema: true);

    /// <inheritdoc />
    public override void SerializeV3(IAsyncApiWriter writer) => Serialize(writer, useV2Schema: false);

    /// <inheritdoc />
    public override void SerializeProperties(IAsyncApiWriter writer) => Serialize(writer, useV2Schema: false);

    private void Serialize(IAsyncApiWriter writer, bool useV2Schema)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteStartObject();
        writer.WriteOptionalProperty("event", Event);
        writer.WriteOptionalProperty("id", Id);
        writer.WriteOptionalProperty("retry", Retry);
        writer.WriteOptionalObject("headers", Headers, (w, s) => SseBindingSerializer.WriteSchema(w, s!, useV2Schema));
        writer.WriteOptionalProperty("bindingVersion", BindingVersion ?? SseProtocol.DefaultBindingVersion);
        writer.WriteExtensions(Extensions);
        writer.WriteEndObject();
    }

    private static int? ParseRetry(string? value) =>
        int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
}
