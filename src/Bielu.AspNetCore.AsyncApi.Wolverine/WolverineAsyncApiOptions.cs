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

    internal bool IsIncluded(Uri uri) =>
        Schemes.Count > 0 ? Schemes.Contains(uri.Scheme) : !_internalSchemes.Contains(uri.Scheme, StringComparer.OrdinalIgnoreCase);

    internal string ServerNameFor(string scheme) => ServerNames.TryGetValue(scheme, out var name) ? name : scheme;
}
