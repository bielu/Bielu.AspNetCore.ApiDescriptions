using Bielu.AspNetCore.AsyncApi.Scalar.Broker.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Scalar.Broker.Tests;

/// <summary>
/// Registration behaviour of the Kafka driver. Nothing here contacts a cluster — the bridge is
/// lazy, which is exactly what lets these run without one.
/// </summary>
public class KafkaBrokerConnectionTests
{
    private static ScalarBrokerBridgeOptions Configure(Action<ScalarBrokerBridgeOptions> configure)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScalarBrokerBridge(configure);
        return services.BuildServiceProvider().GetRequiredService<ScalarBrokerBridgeOptions>();
    }

    [Fact]
    public void AddKafkaConnection_RegistersAKafkaDescriptor()
    {
        // Arrange & Act
        var options = Configure(o => o.AddKafkaConnection("orders", "localhost:9092"));

        // Assert
        options.Connections.Count.ShouldBe(1);
        options.Connections[0].Descriptor.Name.ShouldBe("orders");
        options.Connections[0].Descriptor.Protocol.ShouldBe("kafka");
        options.Connections[0].Descriptor.Endpoint.ShouldBe("localhost:9092");
    }

    [Theory]
    [InlineData("user:secret@broker:9092", "***@broker:9092")]
    [InlineData("broker-a:9092,user:secret@broker-b:9092", "broker-a:9092,***@broker-b:9092")]
    public void AddKafkaConnection_RedactsCredentialsFromTheDisplayEndpoint(string bootstrap, string expected)
    {
        // Arrange — the descriptor is sent to the browser, so it must never carry a password.
        var options = Configure(o => o.AddKafkaConnection("secure", bootstrap));

        // Assert
        options.Connections[0].Descriptor.Endpoint.ShouldBe(expected);
        options.Connections[0].Descriptor.Endpoint.ShouldNotContain("secret");
    }

    [Fact]
    public void AddKafkaConnection_PlainHostPortIsNotMistakenForCredentials()
    {
        // Arrange — `host:port` looks superficially like `user:password`; redacting it would make
        // every ordinary connection unreadable in the console.
        var options = Configure(o => o.AddKafkaConnection("orders", "broker-a:9092,broker-b:9092"));

        // Assert
        options.Connections[0].Descriptor.Endpoint.ShouldBe("broker-a:9092,broker-b:9092");
    }

    [Fact]
    public void AddKafkaConnection_DoesNotConnectAtRegistrationTime()
    {
        // Arrange — an unreachable cluster must not stop the application from starting, so no
        // producer or consumer is built until the console actually calls the bridge.
        var act = () => Configure(o => o.AddKafkaConnection("down", "unreachable.invalid:9092"));

        // Assert
        Should.NotThrow(act);
    }

    [Fact]
    public void AddKafkaConnection_RejectsEmptyArguments()
    {
        // Arrange & Act & Assert
        Should.Throw<ArgumentException>(() => Configure(o => o.AddKafkaConnection("", "localhost:9092")));
        Should.Throw<ArgumentException>(() => Configure(o => o.AddKafkaConnection("orders", "")));
    }
}
