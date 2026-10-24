namespace Bielu.AspNetCore.AsyncApi.CloudEvents;

/// <summary>Options for describing a document's messages as CloudEvents.</summary>
/// <example>
/// <code>
/// options.AddCloudEvents(cloudEvents => cloudEvents
///     .WithSource("/orders")
///     .WithTypePrefix("com.example.orders.")
///     .ConfigureMessage("orderCancelled", message => message.Type = "com.example.orders.cancelled.v2"));
/// </code>
/// </example>
public sealed class CloudEventsOptions
{
    private readonly Dictionary<string, CloudEventsMessageOptions> _messages = new(StringComparer.Ordinal);

    /// <summary>How events travel. Defaults to <see cref="CloudEventsMode.Binary"/>.</summary>
    public CloudEventsMode Mode { get; set; } = CloudEventsMode.Binary;

    /// <summary>
    /// Whose header names binary mode uses. Defaults to <see cref="CloudEventsProtocolBinding.Auto"/>, which picks
    /// one per message from the protocol of its channels' servers.
    /// </summary>
    public CloudEventsProtocolBinding ProtocolBinding { get; set; } = CloudEventsProtocolBinding.Auto;

    /// <summary>
    /// The <c>source</c> every event carries, a URI reference such as <c>/orders</c>. When set, the schema pins it
    /// with <c>const</c>; otherwise it only requires a URI reference.
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// Prepended to a message's name to form its event <c>type</c>, for example <c>com.example.orders.</c>.
    /// </summary>
    public string? TypePrefix { get; set; }

    /// <summary>
    /// Decides which messages are CloudEvents, by their key under <c>components/messages</c>. Every message is when
    /// it is <see langword="null"/>, the default.
    /// </summary>
    public Func<string, bool>? MessageFilter { get; set; }

    internal IReadOnlyDictionary<string, CloudEventsMessageOptions> Messages => _messages;

    /// <summary>Sets <see cref="Source"/>.</summary>
    /// <param name="source">The event source, a URI reference.</param>
    /// <returns>The options, for chaining.</returns>
    public CloudEventsOptions WithSource(string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        Source = source;
        return this;
    }

    /// <summary>Sets <see cref="TypePrefix"/>.</summary>
    /// <param name="typePrefix">The prefix of every event type.</param>
    /// <returns>The options, for chaining.</returns>
    public CloudEventsOptions WithTypePrefix(string typePrefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typePrefix);

        TypePrefix = typePrefix;
        return this;
    }

    /// <summary>Sets <see cref="Mode"/>.</summary>
    /// <param name="mode">How events travel.</param>
    /// <returns>The options, for chaining.</returns>
    public CloudEventsOptions UseMode(CloudEventsMode mode)
    {
        Mode = mode;
        return this;
    }

    /// <summary>Sets <see cref="ProtocolBinding"/>.</summary>
    /// <param name="protocolBinding">Whose header names binary mode uses.</param>
    /// <returns>The options, for chaining.</returns>
    public CloudEventsOptions UseProtocolBinding(CloudEventsProtocolBinding protocolBinding)
    {
        ProtocolBinding = protocolBinding;
        return this;
    }

    /// <summary>Overrides the CloudEvents attributes of one message.</summary>
    /// <param name="messageKey">The message's key under <c>components/messages</c>, for example its <c>MessageId</c>.</param>
    /// <param name="configure">Configures the message's <see cref="CloudEventsMessageOptions"/>.</param>
    /// <returns>The options, for chaining.</returns>
    public CloudEventsOptions ConfigureMessage(string messageKey, Action<CloudEventsMessageOptions> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageKey);
        ArgumentNullException.ThrowIfNull(configure);

        if (!_messages.TryGetValue(messageKey, out var message))
        {
            message = new CloudEventsMessageOptions();
            _messages[messageKey] = message;
        }

        configure(message);
        return this;
    }
}

/// <summary>The CloudEvents attributes of one message, overriding <see cref="CloudEventsOptions"/>.</summary>
public sealed class CloudEventsMessageOptions
{
    /// <summary>The event <c>type</c>. Defaults to <see cref="CloudEventsOptions.TypePrefix"/> plus the message's name.</summary>
    public string? Type { get; set; }

    /// <summary>The event <c>source</c>. Defaults to <see cref="CloudEventsOptions.Source"/>.</summary>
    public string? Source { get; set; }

    /// <summary>The event's <c>dataschema</c>, a URI identifying the schema of its data, if any.</summary>
    public string? DataSchema { get; set; }

    /// <summary>How this message's events travel. Defaults to <see cref="CloudEventsOptions.Mode"/>.</summary>
    public CloudEventsMode? Mode { get; set; }
}
