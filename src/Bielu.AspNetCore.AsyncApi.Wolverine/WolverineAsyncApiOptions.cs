using System.Reflection;

namespace Bielu.AspNetCore.AsyncApi.Wolverine;

/// <summary>
/// Configures how <see cref="WolverineAsyncApiOptionsExtensions.AddWolverine"/> turns Wolverine's
/// message routing into AsyncAPI channels, operations and messages.
/// </summary>
public sealed class WolverineAsyncApiOptions
{
    /// <summary>The SignalR client method Wolverine's SignalR transport delivers messages on.</summary>
    public const string DefaultSignalRClientMethod = "ReceiveMessage";

    private static readonly string[] _internalSchemes = ["local", "stub", "tcp"];

    internal List<Assembly> Assemblies { get; } = [];

    internal HashSet<string> Schemes { get; } = new(StringComparer.OrdinalIgnoreCase);

    internal Dictionary<string, string> ServerNames { get; } = new(StringComparer.OrdinalIgnoreCase);

    internal Dictionary<Uri, string> ChannelAddresses { get; } = [];

    internal bool RepliesIncluded { get; private set; }

    /// <summary>
    /// The client method Wolverine's SignalR transport invokes on connected clients, emitted as the
    /// <c>target</c> of each SignalR operation binding. Defaults to <see cref="DefaultSignalRClientMethod"/>.
    /// </summary>
    public string SignalRClientMethod { get; set; } = DefaultSignalRClientMethod;

    /// <summary>
    /// Adds an assembly whose message types are documented. Only public, concrete types with at least
    /// one route to an included transport end up in the document. When no assembly is added,
    /// Wolverine's application assembly is used.
    /// </summary>
    /// <param name="assembly">The assembly holding message types.</param>
    /// <returns>The same options, for chaining.</returns>
    public WolverineAsyncApiOptions IncludeAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        if (!Assemblies.Contains(assembly))
        {
            Assemblies.Add(assembly);
        }

        return this;
    }

    /// <summary>Adds the assembly that contains <typeparamref name="T"/>; see <see cref="IncludeAssembly"/>.</summary>
    /// <typeparam name="T">Any type from the assembly holding message types.</typeparam>
    /// <returns>The same options, for chaining.</returns>
    public WolverineAsyncApiOptions IncludeAssemblyContaining<T>() => IncludeAssembly(typeof(T).Assembly);

    /// <summary>
    /// Limits the document to endpoints whose URI uses one of <paramref name="schemes"/>, for example
    /// <c>"signalr"</c> or <c>"kafka"</c>. Without this, every transport except Wolverine's
    /// in-process ones (<c>local</c>, <c>stub</c>, <c>tcp</c>) is included.
    /// </summary>
    /// <param name="schemes">The Wolverine endpoint URI schemes to document.</param>
    /// <returns>The same options, for chaining.</returns>
    public WolverineAsyncApiOptions IncludeSchemes(params string[] schemes)
    {
        ArgumentNullException.ThrowIfNull(schemes);
        foreach (var scheme in schemes)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(scheme);
            Schemes.Add(scheme);
        }

        return this;
    }

    /// <summary>
    /// Names the AsyncAPI server that channels on <paramref name="scheme"/> belong to. Defaults to the
    /// scheme itself. The server must be registered with <c>AddServer</c>; channels are only linked to
    /// servers that exist in the document.
    /// </summary>
    /// <param name="scheme">The Wolverine endpoint URI scheme, for example <c>"kafka"</c>.</param>
    /// <param name="serverName">The AsyncAPI server name.</param>
    /// <returns>The same options, for chaining.</returns>
    public WolverineAsyncApiOptions UseServer(string scheme, string serverName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scheme);
        ArgumentException.ThrowIfNullOrWhiteSpace(serverName);
        ServerNames[scheme] = serverName;
        return this;
    }

    /// <summary>
    /// Overrides the channel address for a Wolverine endpoint. Use it when the address can't be derived
    /// from the endpoint: the SignalR transport's single <c>signalr://wolverine/</c> endpoint is
    /// otherwise resolved to the path its hub is mapped at.
    /// </summary>
    /// <param name="endpointUri">The Wolverine endpoint URI, for example <c>signalr://wolverine/</c>.</param>
    /// <param name="address">The AsyncAPI channel address, for example <c>/hubs/chat</c>.</param>
    /// <returns>The same options, for chaining.</returns>
    public WolverineAsyncApiOptions MapChannel(Uri endpointUri, string address)
    {
        ArgumentNullException.ThrowIfNull(endpointUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        ChannelAddresses[endpointUri] = address;
        return this;
    }

    /// <summary>
    /// Documents every message a handler returns as the <c>reply</c> of the listener's <c>receive</c>
    /// operation, including messages that also have a route. Wolverine sends a returned message back to
    /// the endpoint named in the request's <c>reply-uri</c> header when the sender asked for it, for
    /// example with <c>InvokeAsync&lt;T&gt;()</c>, and publishes it through the routing rules otherwise.
    /// Without this, only returned messages that have no route are documented as replies, since a reply
    /// is the only way those can leave the handler. AsyncAPI 3.x only.
    /// </summary>
    /// <returns>The same options, for chaining.</returns>
    public WolverineAsyncApiOptions IncludeReplies()
    {
        RepliesIncluded = true;
        return this;
    }

    /// <summary>
    /// Adds a <c>correlationId</c> to every message, pointing at the <c>correlation-id</c> header Wolverine
    /// stamps on each envelope it sends through a broker.
    /// </summary>
    /// <returns>The same options, for chaining.</returns>
    public WolverineAsyncApiOptions IncludeCorrelationId()
    {
        CorrelationIdIncluded = true;
        return this;
    }

    /// <summary>Adds an <c>externalDocs</c> link to the message for <typeparamref name="TMessage"/>.</summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <param name="url">URL of the external documentation.</param>
    /// <param name="description">An optional description of the documentation.</param>
    /// <returns>The same options, for chaining.</returns>
    public WolverineAsyncApiOptions WithExternalDocs<TMessage>(string url, string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        var metadata = MetadataFor<TMessage>();
        metadata.ExternalDocsUrl = url;
        metadata.ExternalDocsDescription = description;
        return this;
    }

    /// <summary>
    /// Applies message traits to the message for <typeparamref name="TMessage"/>. Each key must be registered
    /// with <c>AsyncApiOptions.AddMessageTrait</c>.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <param name="traitKeys">The keys of the message traits.</param>
    /// <returns>The same options, for chaining.</returns>
    public WolverineAsyncApiOptions WithMessageTraits<TMessage>(params string[] traitKeys)
    {
        MetadataFor<TMessage>().MessageTraits.AddRange(Validated(traitKeys));
        return this;
    }

    /// <summary>
    /// Applies operation traits to every operation of <typeparamref name="TMessage"/>. Each key must be registered
    /// with <c>AsyncApiOptions.AddOperationTrait</c>.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <param name="traitKeys">The keys of the operation traits.</param>
    /// <returns>The same options, for chaining.</returns>
    public WolverineAsyncApiOptions WithOperationTraits<TMessage>(params string[] traitKeys)
    {
        MetadataFor<TMessage>().OperationTraits.AddRange(Validated(traitKeys));
        return this;
    }

    /// <summary>
    /// Requires security schemes on every operation of <typeparamref name="TMessage"/>. Each key must be registered
    /// with <c>AsyncApiOptions.AddSecurityScheme</c>.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <param name="schemeKeys">The keys of the security schemes.</param>
    /// <returns>The same options, for chaining.</returns>
    public WolverineAsyncApiOptions RequireSecurity<TMessage>(params string[] schemeKeys)
    {
        MetadataFor<TMessage>().SecuritySchemes.AddRange(Validated(schemeKeys));
        return this;
    }

    internal bool CorrelationIdIncluded { get; private set; }

    internal Dictionary<Type, MessageMetadata> Metadata { get; } = [];

    private MessageMetadata MetadataFor<TMessage>() =>
        Metadata.TryGetValue(typeof(TMessage), out var metadata) ? metadata : Metadata[typeof(TMessage)] = new MessageMetadata();

    private static string[] Validated(string[] keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        foreach (var key in keys)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
        }

        return keys;
    }

    internal sealed class MessageMetadata
    {
        public string? ExternalDocsUrl { get; set; }

        public string? ExternalDocsDescription { get; set; }

        public List<string> MessageTraits { get; } = [];

        public List<string> OperationTraits { get; } = [];

        public List<string> SecuritySchemes { get; } = [];
    }

    internal bool IsIncluded(Uri uri) =>
        Schemes.Count > 0 ? Schemes.Contains(uri.Scheme) : !_internalSchemes.Contains(uri.Scheme, StringComparer.OrdinalIgnoreCase);

    internal string ServerNameFor(string scheme) => ServerNames.TryGetValue(scheme, out var name) ? name : scheme;
}
