using Bielu.AspNetCore.AsyncApi.Services;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.CloudEvents.Tests.Unit;

public class CloudEventsOptionsTests
{
    [Fact]
    public void Defaults_AreBinaryWithAutoBinding()
    {
        // Act
        var options = new CloudEventsOptions();

        // Assert
        options.Mode.ShouldBe(CloudEventsMode.Binary);
        options.ProtocolBinding.ShouldBe(CloudEventsProtocolBinding.Auto);
        options.Source.ShouldBeNull();
        options.TypePrefix.ShouldBeNull();
        options.MessageFilter.ShouldBeNull();
    }

    [Fact]
    public void FluentMethods_SetTheirPropertiesAndChain()
    {
        // Arrange
        var options = new CloudEventsOptions();

        // Act
        var result = options
            .WithSource("/orders")
            .WithTypePrefix("com.example.")
            .UseMode(CloudEventsMode.Structured)
            .UseProtocolBinding(CloudEventsProtocolBinding.Amqp);

        // Assert
        result.ShouldBeSameAs(options);
        options.Source.ShouldBe("/orders");
        options.TypePrefix.ShouldBe("com.example.");
        options.Mode.ShouldBe(CloudEventsMode.Structured);
        options.ProtocolBinding.ShouldBe(CloudEventsProtocolBinding.Amqp);
    }

    [Fact]
    public void ConfigureMessage_CalledTwice_ConfiguresTheSameMessage()
    {
        // Arrange
        var options = new CloudEventsOptions();

        // Act
        options.ConfigureMessage("placed", message => message.Type = "a.b");
        options.ConfigureMessage("placed", message => message.Source = "/s");

        // Assert
        var message = options.Messages["placed"];
        message.Type.ShouldBe("a.b");
        message.Source.ShouldBe("/s");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void WithSource_ThrowsForMissingSource(string? source)
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => new CloudEventsOptions().WithSource(source!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void WithTypePrefix_ThrowsForMissingPrefix(string? prefix)
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => new CloudEventsOptions().WithTypePrefix(prefix!));
    }

    [Fact]
    public void ConfigureMessage_ThrowsForMissingArguments()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => new CloudEventsOptions().ConfigureMessage("", _ => { }));
        Should.Throw<ArgumentNullException>(() => new CloudEventsOptions().ConfigureMessage("placed", null!));
    }

    [Fact]
    public void AddCloudEvents_ThrowsForNullOptions()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => AsyncApiCloudEventsOptionsExtensions.AddCloudEvents(null!));
    }

    [Fact]
    public void AddCloudEvents_RegistersADocumentTransformer()
    {
        // Arrange
        var options = new AsyncApiOptions();

        // Act
        var result = options.AddCloudEvents();

        // Assert
        result.ShouldBeSameAs(options);
        options.DocumentTransformers.ShouldHaveSingleItem().ShouldBeOfType<CloudEventsDocumentTransformer>();
    }

    [Theory]
    [InlineData("kafka", CloudEventsProtocolBinding.Kafka)]
    [InlineData("kafka-secure", CloudEventsProtocolBinding.Kafka)]
    [InlineData("AMQP", CloudEventsProtocolBinding.Amqp)]
    [InlineData("amqps", CloudEventsProtocolBinding.Amqp)]
    [InlineData("mqtt", CloudEventsProtocolBinding.Mqtt)]
    [InlineData("secure-mqtt", CloudEventsProtocolBinding.Mqtt)]
    [InlineData("https", CloudEventsProtocolBinding.Http)]
    [InlineData("signalr", CloudEventsProtocolBinding.Http)]
    [InlineData(null, CloudEventsProtocolBinding.Http)]
    public void FromProtocol_MapsTheProtocolToItsBinding(string? protocol, CloudEventsProtocolBinding expected)
    {
        // Act & Assert
        CloudEventsDocumentTransformer.FromProtocol(protocol).ShouldBe(expected);
    }
}
