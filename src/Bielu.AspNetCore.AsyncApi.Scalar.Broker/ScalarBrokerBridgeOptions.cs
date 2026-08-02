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

    /// <summary>
    /// The registered connections, in declaration order.
    /// </summary>
    public IReadOnlyList<BrokerConnectionRegistration> Connections => _connections;

    /// <summary>
    /// Registers a broker connection under a logical name.
    /// </summary>
    /// <param name="registration">The connection descriptor and the factory producing its bridge.</param>
    /// <returns>The same options instance for chaining.</returns>
    /// <exception cref="InvalidOperationException">A connection with the same name is already registered.</exception>
    public ScalarBrokerBridgeOptions AddConnection(BrokerConnectionRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);

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
