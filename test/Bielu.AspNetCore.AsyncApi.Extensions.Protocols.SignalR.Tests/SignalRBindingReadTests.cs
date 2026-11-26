using System.Text.Json.Nodes;
using Bielu.AspNetCore.AsyncApi.Extensions.Protocols.SignalR;
using ByteBard.AsyncAPI.Bindings;
using ByteBard.AsyncAPI.Models;
using ByteBard.AsyncAPI.Readers;
using ByteBard.AsyncAPI.Readers.Interface;
using ByteBard.AsyncAPI.Readers.ParseNodes;
using ByteBard.AsyncAPI.Writers;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Extensions.Protocols.SignalR.Tests;

/// <summary>
/// Tests the bindings' parsers (their fixed-field maps) by loading what the writers produce.
/// They call <c>LoadBinding</c> directly: ByteBard's document reader treats every <c>x-</c> key under
/// <c>bindings</c> as an extension, so it never hands an <c>x-signalr</c> binding to these parsers.
/// </summary>
public class SignalRBindingReadTests
{
    private static string Write(AsyncApiBinding binding, bool v2)
    {
        using var stringWriter = new StringWriter();
        var writer = new AsyncApiJsonWriter(stringWriter);
        if (v2)
        {
            binding.SerializeV2(writer);
        }
        else
        {
            binding.SerializeV3(writer);
        }

        stringWriter.Flush();
        return stringWriter.ToString();
    }

    private static T Load<T>(string json)
        where T : IBindingParser<T>, new()
    {
        var diagnostic = new AsyncApiDiagnostic();
        var node = new PropertyNode(new ParsingContext(diagnostic), SignalRProtocol.BindingKey, JsonNode.Parse(json)!);

        var binding = new T().LoadBinding(node);

        diagnostic.Errors.ShouldBeEmpty();
        return binding;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ChannelBinding_RoundTrip_RestoresEveryField(bool v2)
    {
        // Arrange
        var original = new SignalRChannelBinding
        {
            Hub = "/hubs/chat",
            Transports = ["WebSockets", "LongPolling"],
            Protocols = ["json", "messagepack"],
            Query = new AsyncApiJsonSchema { Type = SchemaType.Object },
            Headers = new AsyncApiJsonSchema { Type = SchemaType.Object },
            BindingVersion = "0.2.0",
        };

        // Act
        var binding = Load<SignalRChannelBinding>(Write(original, v2));

        // Assert
        binding.Hub.ShouldBe("/hubs/chat");
        binding.Transports.ShouldBe(["WebSockets", "LongPolling"]);
        binding.Protocols.ShouldBe(["json", "messagepack"]);
        binding.Query.ShouldNotBeNull().Type.ShouldBe(SchemaType.Object);
        binding.Headers.ShouldNotBeNull().Type.ShouldBe(SchemaType.Object);
        binding.BindingVersion.ShouldBe("0.2.0");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MessageBinding_RoundTrip_RestoresEveryField(bool v2)
    {
        // Arrange
        var original = new SignalRMessageBinding
        {
            HubProtocol = "json",
            MessageType = SignalRMessageType.StreamItem,
            Headers = new AsyncApiJsonSchema { Type = SchemaType.Object },
        };

        // Act
        var binding = Load<SignalRMessageBinding>(Write(original, v2));

        // Assert
        binding.HubProtocol.ShouldBe("json");
        binding.MessageType.ShouldBe(SignalRMessageType.StreamItem);
        binding.Headers.ShouldNotBeNull().Type.ShouldBe(SchemaType.Object);
    }

    [Fact]
    public void OperationBinding_RoundTrip_RestoresEveryField()
    {
        // Arrange
        var original = new SignalROperationBinding
        {
            Target = "ReceiveMessage",
            Direction = "serverToClient",
            CallType = "stream",
            Streaming = true,
        };

        // Act
        var binding = Load<SignalROperationBinding>(Write(original, v2: false));

        // Assert
        binding.Target.ShouldBe("ReceiveMessage");
        binding.Direction.ShouldBe("serverToClient");
        binding.CallType.ShouldBe("stream");
        binding.Streaming.ShouldBe(true);
    }

    [Fact]
    public void ServerBinding_RoundTrip_RestoresEveryField()
    {
        // Arrange
        var original = new SignalRServerBinding
        {
            Transports = ["WebSockets"],
            Protocols = ["json"],
        };

        // Act
        var binding = Load<SignalRServerBinding>(Write(original, v2: false));

        // Assert
        binding.Transports.ShouldBe(["WebSockets"]);
        binding.Protocols.ShouldBe(["json"]);
        binding.BindingVersion.ShouldBe(SignalRProtocol.DefaultBindingVersion);
    }

    [Theory]
    [InlineData(SignalRMessageType.Invocation, "invocation")]
    [InlineData(SignalRMessageType.StreamItem, "streamItem")]
    [InlineData(SignalRMessageType.CancelInvocation, "cancelInvocation")]
    [InlineData(SignalRMessageType.Close, "close")]
    public void MessageTypeParse_WireName_RoundTrips(SignalRMessageType type, string wireName)
    {
        type.ToWireName().ShouldBe(wireName);
        SignalRMessageTypeExtensions.Parse(wireName).ShouldBe(type);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("8")]
    [InlineData("handshake")]
    public void MessageTypeParse_UnknownToken_ReturnsNull(string? token)
    {
        SignalRMessageTypeExtensions.Parse(token).ShouldBeNull();
    }

    [Fact]
    public void LoadBinding_UnknownMessageType_LeavesItUnset()
    {
        Load<SignalRMessageBinding>("""{ "messageType": "handshake" }""").MessageType.ShouldBeNull();
    }

    [Fact]
    public void Serialize_NullWriter_Throws()
    {
        Should.Throw<ArgumentNullException>(() => new SignalRChannelBinding().SerializeV3(null!));
        Should.Throw<ArgumentNullException>(() => new SignalROperationBinding().SerializeV3(null!));
        Should.Throw<ArgumentNullException>(() => new SignalRMessageBinding().SerializeV3(null!));
        Should.Throw<ArgumentNullException>(() => new SignalRServerBinding().SerializeV3(null!));
    }

    [Fact]
    public void SerializeProperties_WritesTheSameAsSerializeV3()
    {
        // Arrange
        var bindings = new AsyncApiBinding[]
        {
            new SignalRChannelBinding { Hub = "/hubs/chat" },
            new SignalROperationBinding { Target = "Send" },
            new SignalRMessageBinding { HubProtocol = "json" },
            new SignalRServerBinding { Transports = ["WebSockets"] },
        };

        foreach (var binding in bindings)
        {
            using var stringWriter = new StringWriter();

            // Act
            binding.SerializeProperties(new AsyncApiJsonWriter(stringWriter));

            // Assert
            stringWriter.ToString().ShouldBe(Write(binding, v2: false));
        }
    }
}
