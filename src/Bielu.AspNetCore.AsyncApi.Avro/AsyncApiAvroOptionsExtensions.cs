using Bielu.AspNetCore.AsyncApi.Services;

namespace Bielu.AspNetCore.AsyncApi.Avro;

/// <summary>Describes an AsyncAPI document's message payloads as Avro schemas.</summary>
public static class AsyncApiAvroOptionsExtensions
{
    /// <summary>
    /// Replaces the JSON Schema payload of every message (or those <see cref="AvroPayloadOptions.MessageFilter"/>
    /// keeps) with the equivalent Avro schema, declared as <c>schemaFormat: application/vnd.apache.avro;version=1.9.0</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Objects become records named after their schema and properties become fields. A nullable property becomes a
    /// union with <c>null</c>, and one that is not required also defaults to <c>null</c>. Arrays, dictionaries (maps), string enums,
    /// and the <c>uuid</c>, <c>date-time</c>, <c>date</c> and <c>time</c> formats (logical types) are kept. An
    /// <c>integer</c> becomes an <c>int</c> with the <c>int32</c> format and a <c>long</c> otherwise.
    /// </para>
    /// <para>
    /// The payload's JSON Schema stays under <c>components/schemas</c>. Headers are left as JSON Schema, since Avro
    /// describes only the payload. A payload already in another schema format is left alone. The transformer runs where
    /// it is added among the document transformers, so add it after anything that adds messages.
    /// </para>
    /// </remarks>
    /// <param name="options">The options of the document to describe.</param>
    /// <param name="configure">Configures the <see cref="AvroPayloadOptions"/>.</param>
    /// <returns>The <see cref="AsyncApiOptions"/> instance for further customization.</returns>
    /// <example>
    /// <code>
    /// builder.Services.AddAsyncApi(options => options
    ///     .AddServer("kafka", "broker:9092", "kafka")
    ///     .AddAvroPayloads(avro => avro.WithNamespace("com.example.orders")));
    /// </code>
    /// </example>
    public static AsyncApiOptions AddAvroPayloads(this AsyncApiOptions options, Action<AvroPayloadOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        var avroOptions = new AvroPayloadOptions();
        configure?.Invoke(avroOptions);
        ArgumentException.ThrowIfNullOrWhiteSpace(avroOptions.AvroVersion, nameof(AvroPayloadOptions.AvroVersion));
        return options.AddDocumentTransformer(new AvroPayloadDocumentTransformer(avroOptions));
    }
}
