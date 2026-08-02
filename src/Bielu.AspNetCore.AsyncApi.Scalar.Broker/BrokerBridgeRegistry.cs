using System.Diagnostics.CodeAnalysis;

namespace Bielu.AspNetCore.AsyncApi.Scalar.Broker;

/// <summary>
/// Looks up the <see cref="IBrokerBridge" /> serving a named connection, and lists the connections
/// the console may offer.
/// </summary>
public interface IBrokerBridgeRegistry
{
    /// <summary>
    /// The registered connections, as the console sees them. Never carries credentials.
    /// </summary>
    IReadOnlyList<BrokerConnectionDescriptor> Connections { get; }

    /// <summary>
    /// Resolves the bridge for a connection name.
    /// </summary>
    /// <param name="connectionName">The logical connection name.</param>
    /// <param name="bridge">The bridge, when the name is registered.</param>
    /// <returns><see langword="true" /> when the connection is registered.</returns>
    bool TryGetBridge(string connectionName, [NotNullWhen(true)] out IBrokerBridge? bridge);
}

/// <summary>
/// Default <see cref="IBrokerBridgeRegistry" />: builds each connection's bridge on first use and
/// keeps it for the lifetime of the application.
/// </summary>
internal sealed class BrokerBridgeRegistry : IBrokerBridgeRegistry, IAsyncDisposable
{
    private readonly Dictionary<string, Lazy<IBrokerBridge>> _bridges;

    public BrokerBridgeRegistry(IServiceProvider services, ScalarBrokerBridgeOptions options)
    {
        Connections = options.Connections.Select(registration => registration.Descriptor).ToArray();

        // Lazy so a broker that is unreachable at startup does not stop the application from
        // starting - the console is a diagnostic tool, not a dependency of the app it is hosted in.
        // LazyThreadSafetyMode default (ExecutionAndPublication) keeps a single bridge per name even
        // under concurrent first requests.
        _bridges = options.Connections.ToDictionary(
            registration => registration.Descriptor.Name,
            registration => new Lazy<IBrokerBridge>(() => registration.BridgeFactory(services)),
            StringComparer.Ordinal);
    }

    public IReadOnlyList<BrokerConnectionDescriptor> Connections { get; }

    public bool TryGetBridge(string connectionName, [NotNullWhen(true)] out IBrokerBridge? bridge)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionName);

        if (_bridges.TryGetValue(connectionName, out var lazy))
        {
            bridge = lazy.Value;
            return true;
        }

        bridge = null;
        return false;
    }

    public async ValueTask DisposeAsync()
    {
        // Only dispose bridges that were actually built; touching Lazy.Value here would connect to
        // brokers during shutdown purely to disconnect from them again.
        foreach (var lazy in _bridges.Values.Where(static lazy => lazy.IsValueCreated))
        {
            await lazy.Value.DisposeAsync();
        }
    }
}
