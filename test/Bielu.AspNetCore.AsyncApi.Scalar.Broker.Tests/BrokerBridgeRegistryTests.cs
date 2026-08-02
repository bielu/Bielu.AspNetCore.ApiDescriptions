using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Scalar.Broker.Tests;

/// <summary>
/// Registration and lifetime of the bridges behind each connection.
/// </summary>
public class BrokerBridgeRegistryTests
{
    private static IBrokerBridgeRegistry Registry(Action<ScalarBrokerBridgeOptions> configure)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScalarBrokerBridge(configure);
        return services.BuildServiceProvider().GetRequiredService<IBrokerBridgeRegistry>();
    }

    private static BrokerConnectionRegistration Registration(
        string name,
        Func<IServiceProvider, IBrokerBridge> factory) =>
        new(new BrokerConnectionDescriptor(name, "kafka", "localhost:9092"), factory);

    [Fact]
    public void AddConnection_DuplicateName_Throws()
    {
        // Arrange — names address the connection on the proxy endpoints, so a duplicate would make
        // one of the two unreachable rather than merely being untidy.
        var services = new ServiceCollection();

        // Act
        var act = () => services.AddScalarBrokerBridge(options =>
        {
            options.AddConnection(Registration("orders", _ => new FakeBrokerBridge()));
            options.AddConnection(Registration("orders", _ => new FakeBrokerBridge()));
        });

        // Assert
        var exception = Should.Throw<InvalidOperationException>(act);
        exception.Message.ShouldContain("orders");
    }

    [Fact]
    public void Bridges_AreNotBuiltUntilFirstUse()
    {
        // Arrange — a broker that is down at startup must not stop the app from starting.
        var built = 0;
        var registry = Registry(options =>
            options.AddConnection(Registration("orders", _ =>
            {
                built++;
                return new FakeBrokerBridge();
            })));

        // Assert — listing connections reads descriptors only, so nothing has connected yet.
        registry.Connections.Count.ShouldBe(1);
        built.ShouldBe(0);

        // Act
        registry.TryGetBridge("orders", out _).ShouldBeTrue();

        // Assert
        built.ShouldBe(1);
    }

    [Fact]
    public void Bridge_IsBuiltOnceAndReused()
    {
        // Arrange
        var built = 0;
        var registry = Registry(options =>
            options.AddConnection(Registration("orders", _ =>
            {
                built++;
                return new FakeBrokerBridge();
            })));

        // Act
        registry.TryGetBridge("orders", out var first);
        registry.TryGetBridge("orders", out var second);

        // Assert
        built.ShouldBe(1);
        second.ShouldBeSameAs(first);
    }

    [Fact]
    public void TryGetBridge_UnknownName_ReturnsFalse()
    {
        // Arrange
        var registry = Registry(options =>
            options.AddConnection(Registration("orders", _ => new FakeBrokerBridge())));

        // Act
        var found = registry.TryGetBridge("nope", out var bridge);

        // Assert
        found.ShouldBeFalse();
        bridge.ShouldBeNull();
    }

    [Fact]
    public async Task Dispose_OnlyDisposesBridgesThatWereBuilt()
    {
        // Arrange — disposing an unbuilt bridge would mean connecting to a broker during shutdown
        // purely to disconnect again.
        var used = new FakeBrokerBridge();
        var unused = new FakeBrokerBridge();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScalarBrokerBridge(options =>
        {
            options.AddConnection(Registration("used", _ => used));
            options.AddConnection(Registration("unused", _ => unused));
        });
        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IBrokerBridgeRegistry>().TryGetBridge("used", out _);

        // Act
        await provider.DisposeAsync();

        // Assert
        used.Disposed.ShouldBeTrue();
        unused.Disposed.ShouldBeFalse();
    }
}
