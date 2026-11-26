using System.Text.Json.Nodes;
using Bielu.AspNetCore.AsyncApi.Extensions.Protocols.WebRtc;
using ByteBard.AsyncAPI.Bindings;
using ByteBard.AsyncAPI.Models;
using ByteBard.AsyncAPI.Readers;
using ByteBard.AsyncAPI.Readers.Interface;
using ByteBard.AsyncAPI.Readers.ParseNodes;
using ByteBard.AsyncAPI.Writers;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Extensions.Protocols.WebRtc.Tests;

/// <summary>
/// Tests the bindings' parsers (their fixed-field maps) by loading what the writers produce.
/// They call <c>LoadBinding</c> directly: ByteBard's document reader treats every <c>x-</c> key under
/// <c>bindings</c> as an extension, so it never hands an <c>x-webrtc</c> binding to these parsers.
/// </summary>
public class WebRtcBindingReadTests
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
        var node = new PropertyNode(new ParsingContext(diagnostic), WebRtcProtocol.BindingKey, JsonNode.Parse(json)!);

        var binding = new T().LoadBinding(node);

        diagnostic.Errors.ShouldBeEmpty();
        return binding;
    }

    [Fact]
    public void ChannelBinding_RoundTrip_RestoresEveryField()
    {
        // Arrange
        var original = new WebRtcChannelBinding
        {
            ChannelType = WebRtcProtocol.ChannelTypes.DataChannel,
            Label = "chat",
            SubProtocol = "chat.v1",
            Ordered = false,
            MaxRetransmits = 3,
            MaxPacketLifeTime = 500,
            Negotiated = true,
            Id = 7,
            BindingVersion = "0.2.0",
        };

        // Act
        var binding = Load<WebRtcChannelBinding>(Write(original, v2: false));

        // Assert
        binding.ChannelType.ShouldBe(WebRtcProtocol.ChannelTypes.DataChannel);
        binding.Label.ShouldBe("chat");
        binding.SubProtocol.ShouldBe("chat.v1");
        binding.Ordered.ShouldBe(false);
        binding.MaxRetransmits.ShouldBe(3);
        binding.MaxPacketLifeTime.ShouldBe(500);
        binding.Negotiated.ShouldBe(true);
        binding.Id.ShouldBe(7);
        binding.BindingVersion.ShouldBe("0.2.0");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MessageBinding_RoundTrip_RestoresEveryField(bool v2)
    {
        // Arrange
        var original = new WebRtcMessageBinding
        {
            SignalingType = WebRtcSignalingType.Candidate,
            Encoding = WebRtcProtocol.Encodings.Json,
            Headers = new AsyncApiJsonSchema { Type = SchemaType.Object },
        };

        // Act
        var binding = Load<WebRtcMessageBinding>(Write(original, v2));

        // Assert
        binding.SignalingType.ShouldBe(WebRtcSignalingType.Candidate);
        binding.Encoding.ShouldBe(WebRtcProtocol.Encodings.Json);
        binding.Headers.ShouldNotBeNull().Type.ShouldBe(SchemaType.Object);
    }

    [Fact]
    public void OperationBinding_RoundTrip_RestoresEveryField()
    {
        // Arrange
        var original = new WebRtcOperationBinding
        {
            SignalingType = WebRtcSignalingType.Offer,
            Direction = WebRtcProtocol.Directions.Bidirectional,
        };

        // Act
        var binding = Load<WebRtcOperationBinding>(Write(original, v2: false));

        // Assert
        binding.SignalingType.ShouldBe(WebRtcSignalingType.Offer);
        binding.Direction.ShouldBe(WebRtcProtocol.Directions.Bidirectional);
        binding.BindingVersion.ShouldBe(WebRtcProtocol.DefaultBindingVersion);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ServerBinding_RoundTrip_RestoresEveryField(bool v2)
    {
        // Arrange
        var original = new WebRtcServerBinding
        {
            SignalingUrl = "wss://signal.example.com",
            IceServers = ["stun:stun.example.com:3478", "turn:turn.example.com"],
            BundlePolicy = WebRtcProtocol.BundlePolicies.MaxBundle,
        };

        // Act
        var binding = Load<WebRtcServerBinding>(Write(original, v2));

        // Assert
        binding.SignalingUrl.ShouldBe("wss://signal.example.com");
        binding.IceServers.ShouldBe(["stun:stun.example.com:3478", "turn:turn.example.com"]);
        binding.BundlePolicy.ShouldBe(WebRtcProtocol.BundlePolicies.MaxBundle);
    }

    [Fact]
    public void LoadBinding_InvalidNumbersAndSignalingType_LeavesThemUnset()
    {
        // Arrange
        const string channelJson = """{ "maxRetransmits": "many", "maxPacketLifeTime": "", "id": "x" }""";
        const string messageJson = """{ "signalingType": "renegotiate" }""";

        // Act
        var channel = Load<WebRtcChannelBinding>(channelJson);
        var message = Load<WebRtcMessageBinding>(messageJson);

        // Assert
        channel.MaxRetransmits.ShouldBeNull();
        channel.MaxPacketLifeTime.ShouldBeNull();
        channel.Id.ShouldBeNull();
        message.SignalingType.ShouldBeNull();
    }

    [Theory]
    [InlineData("offer", WebRtcSignalingType.Offer)]
    [InlineData("ANSWER", WebRtcSignalingType.Answer)]
    [InlineData("candidate", WebRtcSignalingType.Candidate)]
    public void SignalingTypeParse_KnownToken_ReturnsType(string token, WebRtcSignalingType expected)
    {
        WebRtcSignalingTypeExtensions.Parse(token).ShouldBe(expected);
        expected.ToWireName().ShouldBe(token.ToLowerInvariant());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("99")]
    [InlineData("hangup")]
    public void SignalingTypeParse_UnknownToken_ReturnsNull(string? token)
    {
        WebRtcSignalingTypeExtensions.Parse(token).ShouldBeNull();
    }

    [Fact]
    public void Serialize_NullWriter_Throws()
    {
        Should.Throw<ArgumentNullException>(() => new WebRtcChannelBinding().SerializeV3(null!));
        Should.Throw<ArgumentNullException>(() => new WebRtcOperationBinding().SerializeV3(null!));
        Should.Throw<ArgumentNullException>(() => new WebRtcMessageBinding().SerializeV3(null!));
        Should.Throw<ArgumentNullException>(() => new WebRtcServerBinding().SerializeV3(null!));
    }

    [Fact]
    public void SerializeProperties_WritesTheSameAsSerializeV3()
    {
        // Arrange
        var bindings = new AsyncApiBinding[]
        {
            new WebRtcChannelBinding { Label = "chat" },
            new WebRtcOperationBinding { Direction = WebRtcProtocol.Directions.ClientToServer },
            new WebRtcMessageBinding { Encoding = WebRtcProtocol.Encodings.Text },
            new WebRtcServerBinding { SignalingUrl = "wss://signal" },
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
