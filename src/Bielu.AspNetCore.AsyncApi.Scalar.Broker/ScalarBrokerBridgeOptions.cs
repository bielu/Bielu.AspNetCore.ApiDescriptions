using Microsoft.Extensions.DependencyInjection;

namespace Bielu.AspNetCore.AsyncApi.Scalar.Broker;

/// <summary>
/// Builder for the broker connections the console can reach. Driver packages add their own
/// <c>Add{Protocol}Connection</c> extension methods on top of <see cref="AddConnection" />.
/// </summary>
public sealed class ScalarBrokerBridgeOptions
{
    private readonly List<BrokerConnectionRegistration> _connections = [];

    internal ScalarBrokerBridgeOptions(IServiceCollection services)
    {
        Services = services;
    }

    /// <summary>
    /// The service collection the bridge is being registered into, so driver packages can register
    /// their own supporting services (clients, option types, health checks).
    /// </summary>
    public IServiceCollection Services { get; }

    /// <summary>
    /// When <see langword="true" />, the proxy endpoints serve unauthenticated callers outside the
    /// Development environment. Defaults to <see langword="false" />.
    /// </summary>
    /// <remarks>
    /// This bridge grants publish access to your broker to anyone who can reach the endpoints, so it
    /// does not open itself up by default. Prefer
    /// <c>MapScalarBrokerAssets().RequireAuthorization(...)</c>; set this only when the endpoints are
    /// protected by something ASP.NET Core authorization cannot see, such as a network boundary.
    /// </remarks>
    public bool AllowAnonymous { get; set; }

    private readonly HashSet<string> _allowedChannels = new(StringComparer.Ordinal);

    /// <summary>
    /// The registered connections, in declaration order.
    /// </summary>
    public IReadOnlyList<BrokerConnectionRegistration> Connections => _connections;

    /// <summary>
    /// The channels the proxy may publish to and tail. Empty, the default, allows every channel.
    /// </summary>
    /// <remarks>
    /// Authorization decides <em>who</em> may use the proxy; this decides <em>what</em> they can reach. Without
    /// it, a caller who passes authorization can publish to and read any topic the connection's credentials can,
    /// not only the channels your AsyncAPI documents describe. Names are compared exactly, as the console sends
    /// them: the channel's address, or the topic its binding names.
    /// </remarks>
    public IReadOnlySet<string> AllowedChannels => _allowedChannels;

    /// <summary>
    /// Restricts the proxy to the given channels, in addition to any allowed already. Requests for any other
    /// channel are refused with <c>403</c>.
    /// </summary>
    /// <param name="channels">The channel names to allow, compared exactly.</param>
    /// <returns>The same options instance for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="channels" /> is null.</exception>
    /// <exception cref="ArgumentException">A channel name is null, empty, or whitespace-only.</exception>
    /// <example>
    /// <code>
    /// builder.Services.AddScalarBrokerBridge(broker => broker
    ///     .AllowChannels("orders.created", "orders.cancelled")
    ///     .AddKafkaConnection("orders", "localhost:9092"));
    /// </code>
    /// </example>
    public ScalarBrokerBridgeOptions AllowChannels(params string[] channels)
    {
        ArgumentNullException.ThrowIfNull(channels);

        foreach (var channel in channels)
        {
            if (string.IsNullOrWhiteSpace(channel))
            {
                throw new ArgumentException("A channel name must not be null, empty, or whitespace-only.", nameof(channels));
            }
        }

        _allowedChannels.UnionWith(channels);
        return this;
    }

    /// <summary>
    /// Whether the proxy may publish to or tail <paramref name="channel" />.
    /// </summary>
    internal bool IsChannelAllowed(string channel) => _allowedChannels.Count == 0 || _allowedChannels.Contains(channel);

    /// <summary>
    /// Registers a broker connection under a logical name.
    /// </summary>
    /// <param name="registration">The connection descriptor and the factory producing its bridge.</param>
    /// <returns>The same options instance for chaining.</returns>
    /// <exception cref="ArgumentException"><paramref name="registration" />'s connection name is null, empty, or whitespace-only.</exception>
    /// <exception cref="InvalidOperationException">A connection with the same name is already registered.</exception>
    public ScalarBrokerBridgeOptions AddConnection(BrokerConnectionRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);

        // A whitespace-only name would register and list, but TryGetBridge rejects only empty
        // strings, so it could never actually be resolved back to a bridge.
        if (string.IsNullOrWhiteSpace(registration.Descriptor.Name))
        {
            throw new ArgumentException(
                "A broker connection name must not be null, empty, or whitespace-only.",
                nameof(registration));
        }

        if (_connections.Any(existing => string.Equals(existing.Descriptor.Name, registration.Descriptor.Name, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"A broker connection named '{registration.Descriptor.Name}' is already registered. Connection names address the connection on the proxy endpoints, so they must be unique.");
        }

        _connections.Add(registration);
        return this;
    }
}

/// <summary>
/// A registered broker connection: how it is described to the console, and how its bridge is built.
/// </summary>
/// <param name="Descriptor">The credential-free description the console sees.</param>
/// <param name="BridgeFactory">Creates the bridge serving this connection. Invoked once, lazily.</param>
public sealed record BrokerConnectionRegistration(
    BrokerConnectionDescriptor Descriptor,
    Func<IServiceProvider, IBrokerBridge> BridgeFactory);
