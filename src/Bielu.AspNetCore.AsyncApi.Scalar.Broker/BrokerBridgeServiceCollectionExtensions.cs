using Microsoft.Extensions.DependencyInjection;

namespace Bielu.AspNetCore.AsyncApi.Scalar.Broker;

/// <summary>
/// Registers the server-side broker bridge the interactive console publishes and tails through.
/// </summary>
public static class BrokerBridgeServiceCollectionExtensions
{
    /// <summary>
    /// Registers the broker bridge and the connections the console may reach.
    /// </summary>
    /// <remarks>
    /// Nothing is exposed over HTTP by this call alone — the proxy endpoints only exist once
    /// <c>MapScalarBrokerAssets()</c> is called, and that call is where authorization is applied.
    /// Add connections with a driver package's extension method, for example
    /// <c>options.AddKafkaConnection("orders", "localhost:9092")</c>.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Declares the broker connections.</param>
    /// <returns>The same <see cref="IServiceCollection" /> for chaining.</returns>
    public static IServiceCollection AddScalarBrokerBridge(
        this IServiceCollection services,
        Action<ScalarBrokerBridgeOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new ScalarBrokerBridgeOptions(services);
        configure(options);

        services.AddSingleton(options);
        services.AddSingleton<IBrokerBridgeRegistry>(provider =>
            new BrokerBridgeRegistry(provider, provider.GetRequiredService<ScalarBrokerBridgeOptions>()));
        services.AddSingleton<BrokerBridgeAccessGuard>();

        return services;
    }
}
