using ByteBard.AsyncAPI.Bindings;
using ByteBard.AsyncAPI.Models;
using ByteBard.AsyncAPI.Readers;
using ByteBard.AsyncAPI.Readers.ParseNodes;
using ByteBard.AsyncAPI.Writers;

namespace Bielu.AspNetCore.AsyncApi.Extensions.Protocols.Sse;

/// <summary>
/// Channel binding for the <c>sse</c> protocol. A channel maps to a single event-stream endpoint
/// (a route that responds with <c>text/event-stream</c>).
/// </summary>
public class SseChannelBinding : ChannelBinding<SseChannelBinding>
{
    /// <summary>The endpoint path the stream is served from, for example <c>/events</c>.</summary>
    public string? Path { get; set; }

    /// <summary>The HTTP method used to open the stream (see <see cref="SseProtocol.Methods"/>).</summary>
    public string? Method { get; set; }

    /// <summary>The streamed media type (defaults to <see cref="SseProtocol.EventStreamContentType"/>).</summary>
    public string? ContentType { get; set; }

    /// <summary>Schema describing the connection query string (for example a <c>topic</c> filter).</summary>
    public AsyncApiJsonSchema? Query { get; set; }

    /// <summary>Schema describing request headers (for example <c>Last-Event-ID</c> used to resume a stream).</summary>
    public AsyncApiJsonSchema? Headers { get; set; }

    /// <inheritdoc />
    public override string BindingKey => SseProtocol.ProtocolName;

    /// <inheritdoc />
    protected override FixedFieldMap<SseChannelBinding> FixedFieldMap => new()
    {
        { "bindingVersion", (a, n) => a.BindingVersion = n.GetScalarValue() },
        { "path", (a, n) => a.Path = n.GetScalarValue() },
        { "method", (a, n) => a.Method = n.GetScalarValue() },
        { "contentType", (a, n) => a.ContentType = n.GetScalarValue() },
        { "query", (a, n) => a.Query = AsyncApiJsonSchemaDeserializer.LoadSchema(n) },
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
        writer.WriteOptionalProperty("path", Path);
        writer.WriteOptionalProperty("method", Method);
        writer.WriteOptionalProperty("contentType", ContentType ?? SseProtocol.EventStreamContentType);
        writer.WriteOptionalObject("query", Query, (w, s) => SseBindingSerializer.WriteSchema(w, s!, useV2Schema));
        writer.WriteOptionalObject("headers", Headers, (w, s) => SseBindingSerializer.WriteSchema(w, s!, useV2Schema));
        writer.WriteOptionalProperty("bindingVersion", BindingVersion ?? SseProtocol.DefaultBindingVersion);
        writer.WriteExtensions(Extensions);
        writer.WriteEndObject();
    }
}
