namespace Bielu.AspNetCore.AsyncApi.Protobuf;

/// <summary>Options for describing Protocol Buffers payloads as <c>.proto</c> schemas.</summary>
/// <example>
/// <code>
/// options.AddProtobufPayloads(protobuf => protobuf.ForMessages("helloRequest"));
/// </code>
/// </example>
public sealed class ProtobufPayloadOptions
{
    /// <summary>
    /// Decides which messages with a Protocol Buffers payload get a <c>.proto</c> schema, by their key under
    /// <c>components/messages</c>. Every one does when it is <see langword="null"/>, the default.
    /// </summary>
    public Func<string, bool>? MessageFilter { get; set; }

    /// <summary>Limits the <c>.proto</c> schemas to the messages with these keys under <c>components/messages</c>.</summary>
    /// <param name="messageKeys">The message keys, for example message ids.</param>
    /// <returns>The options, for chaining.</returns>
    public ProtobufPayloadOptions ForMessages(params string[] messageKeys)
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
