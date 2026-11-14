using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Scalar.Broker.Tests.Unit;

/// <summary>
/// <see cref="ScalarBrokerBridgeOptions.AllowChannels" />, which limits the channels the proxy reaches.
/// </summary>
public class ScalarBrokerBridgeOptionsTests
{
    private static ScalarBrokerBridgeOptions Options(Action<ScalarBrokerBridgeOptions> configure)
    {
        ScalarBrokerBridgeOptions? captured = null;
        new ServiceCollection().AddScalarBrokerBridge(options =>
        {
            configure(options);
            captured = options;
        });
        return captured!;
    }

    [Fact]
    public void AllowedChannels_ByDefault_IsEmpty()
    {
        // Act
        var options = Options(_ => { });

        // Assert
        options.AllowedChannels.ShouldBeEmpty();
    }

    [Fact]
    public void AllowChannels_CalledTwice_AccumulatesChannels()
    {
        // Act
        var options = Options(broker => broker.AllowChannels("orders.created").AllowChannels("orders.cancelled", "orders.created"));

        // Assert
        options.AllowedChannels.OrderBy(channel => channel).ShouldBe(["orders.cancelled", "orders.created"]);
    }

    [Fact]
    public void AllowChannels_ThrowsForNullArray()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => Options(broker => broker.AllowChannels(null!)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AllowChannels_ThrowsForEmptyOrWhitespaceName(string channel)
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => Options(broker => broker.AllowChannels("orders.created", channel)));
    }
}
