using System.Text.RegularExpressions;

namespace Bielu.AspNetCore.AsyncApi.Scalar.Broker.Kafka;

/// <summary>
/// Registers Kafka connections on the Scalar broker console bridge.
/// </summary>
public static partial class KafkaBrokerBridgeOptionsExtensions
{
    /// <summary>
    /// Adds a Kafka connection the console can publish to and tail.
    /// </summary>
    /// <remarks>
    /// The bridge is built on first use, so an unreachable cluster does not stop the application
    /// from starting. Tailing uses a throwaway consumer group starting at the newest offset and
    /// never commits, so opening a console does not move any real consumer group's position.
    /// </remarks>
    /// <param name="options">The bridge options being configured.</param>
    /// <param name="name">The logical connection name the console addresses this cluster by.</param>
    /// <param name="bootstrapServers">The Kafka <c>bootstrap.servers</c> value.</param>
    /// <param name="configure">Optional producer/consumer configuration (SASL, SSL, timeouts).</param>
    /// <returns>The same options instance for chaining.</returns>
    public static ScalarBrokerBridgeOptions AddKafkaConnection(
        this ScalarBrokerBridgeOptions options,
        string name,
        string bootstrapServers,
        Action<KafkaConnectionOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(bootstrapServers);

        var connectionOptions = new KafkaConnectionOptions();
        configure?.Invoke(connectionOptions);

        return options.AddConnection(new BrokerConnectionRegistration(
            new BrokerConnectionDescriptor(name, "kafka", Redact(bootstrapServers)),
            _ => new KafkaBrokerBridge(bootstrapServers, connectionOptions)));
    }

    /// <summary>
    /// Strips any <c>user:password@</c> credentials before the endpoint is shown in a browser.
    /// </summary>
    /// <remarks>
    /// Kafka credentials normally live in SASL configuration rather than in
    /// <c>bootstrap.servers</c>, but the descriptor is sent to the console verbatim, so a
    /// URL-shaped value carrying credentials must not leak through it.
    /// </remarks>
    private static string Redact(string bootstrapServers) =>
        CredentialsPattern().Replace(bootstrapServers, "$1***@");

    [GeneratedRegex(@"(^|,)\s*[^,:/@\s]+:[^,@\s]*@", RegexOptions.None, matchTimeoutMilliseconds: 200)]
    private static partial Regex CredentialsPattern();
}
