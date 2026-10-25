namespace Bielu.AspNetCore.AsyncApi.Avro;

/// <summary>Options for describing message payloads as Avro schemas.</summary>
/// <example>
/// <code>
/// options.AddAvroPayloads(avro => avro
///     .WithNamespace("com.example.orders")
///     .ForMessages("orderPlaced", "orderCancelled"));
/// </code>
/// </example>
public sealed class AvroPayloadOptions
{
    /// <summary>The Avro specification version declared in the <c>schemaFormat</c>.</summary>
    public const string DefaultAvroVersion = "1.9.0";

    /// <summary>The namespace of every named Avro type (records and enums), for example <c>com.example.orders</c>.</summary>
    public string? Namespace { get; set; }

    /// <summary>
    /// The Avro version declared in each payload's <c>schemaFormat</c>,
    /// <c>application/vnd.apache.avro;version=…</c>. Defaults to <see cref="DefaultAvroVersion"/>.
    /// </summary>
    public string AvroVersion { get; set; } = DefaultAvroVersion;

    /// <summary>
    /// Decides which messages get an Avro payload, by their key under <c>components/messages</c>. Every message does
    /// when it is <see langword="null"/>, the default.
    /// </summary>
    public Func<string, bool>? MessageFilter { get; set; }

    /// <summary>Sets <see cref="Namespace"/>.</summary>
    /// <param name="avroNamespace">The namespace of named Avro types.</param>
    /// <returns>The options, for chaining.</returns>
    public AvroPayloadOptions WithNamespace(string avroNamespace)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(avroNamespace);

        Namespace = avroNamespace;
        return this;
    }

    /// <summary>Limits the Avro payloads to the messages with these keys under <c>components/messages</c>.</summary>
    /// <param name="messageKeys">The message keys, for example message ids.</param>
    /// <returns>The options, for chaining.</returns>
    public AvroPayloadOptions ForMessages(params string[] messageKeys)
    {
        ArgumentNullException.ThrowIfNull(messageKeys);
        foreach (var key in messageKeys)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
        }

        var keys = new HashSet<string>(messageKeys, StringComparer.Ordinal);
        MessageFilter = keys.Contains;
        return this;
    }
}
