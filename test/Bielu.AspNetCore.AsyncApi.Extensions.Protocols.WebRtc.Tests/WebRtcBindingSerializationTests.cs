using System.Text.Json.Nodes;
using Bielu.AspNetCore.AsyncApi.Extensions.Protocols.WebRtc;
using ByteBard.AsyncAPI.Bindings;
using ByteBard.AsyncAPI.Writers;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Extensions.Protocols.WebRtc.Tests;

public class WebRtcBindingSerializationTests
{
    private static JsonNode SerializeV3(AsyncApiBinding binding)
    {
        using var stringWriter = new StringWriter();
        var writer = new AsyncApiJsonWriter(stringWriter);
        binding.SerializeV3(writer);
        stringWriter.Flush();
        return JsonNode.Parse(stringWriter.ToString())!;
    }

    private static JsonNode SerializeV2(AsyncApiBinding binding)
    {
        using var stringWriter = new StringWriter();
        var writer = new AsyncApiJsonWriter(stringWriter);
        binding.SerializeV2(writer);
        stringWriter.Flush();
        return JsonNode.Parse(stringWriter.ToString())!;
    }

    [Fact]
    public void AllBindings_UseWebRtcBindingKey()
    {
        new WebRtcChannelBinding().BindingKey.ShouldBe("webrtc");
        new WebRtcOperationBinding().BindingKey.ShouldBe("webrtc");
        new WebRtcMessageBinding().BindingKey.ShouldBe("webrtc");
        new WebRtcServerBinding().BindingKey.ShouldBe("webrtc");
    }

    [Fact]
    public void ChannelBinding_SerializesDataChannelFields()
    {
        var binding = new WebRtcChannelBinding
        {
            ChannelType = WebRtcProtocol.ChannelTypes.DataChannel,
            Label = "chat",
            SubProtocol = "json",
            Ordered = true,
            MaxRetransmits = 3,
            Negotiated = false,
            Id = 7,
        };

        var json = SerializeV3(binding);

        json["channelType"]!.GetValue<string>().ShouldBe("dataChannel");
        json["label"]!.GetValue<string>().ShouldBe("chat");
        json["subProtocol"]!.GetValue<string>().ShouldBe("json");
        json["ordered"]!.GetValue<bool>().ShouldBeTrue();
        json["maxRetransmits"]!.GetValue<int>().ShouldBe(3);
        json["id"]!.GetValue<int>().ShouldBe(7);
        json["bindingVersion"]!.GetValue<string>().ShouldBe(WebRtcProtocol.DefaultBindingVersion);
    }

    [Fact]
    public void OperationBinding_SerializesSignalingType()
    {
        var binding = new WebRtcOperationBinding
        {
            SignalingType = WebRtcSignalingType.Offer,
            Direction = WebRtcProtocol.Directions.ClientToServer,
        };

        var json = SerializeV3(binding);

        json["signalingType"]!.GetValue<string>().ShouldBe("offer");
        json["direction"]!.GetValue<string>().ShouldBe("clientToServer");
    }

    [Fact]
    public void MessageBinding_SerializesSignalingAndEncoding()
    {
        var binding = new WebRtcMessageBinding
        {
            SignalingType = WebRtcSignalingType.Candidate,
            Encoding = WebRtcProtocol.Encodings.Json,
        };

        var json = SerializeV3(binding);

        json["signalingType"]!.GetValue<string>().ShouldBe("candidate");
        json["encoding"]!.GetValue<string>().ShouldBe("json");
    }

    [Theory]
    [InlineData(WebRtcSignalingType.Offer, "offer")]
    [InlineData(WebRtcSignalingType.Answer, "answer")]
    [InlineData(WebRtcSignalingType.Candidate, "candidate")]
    public void SignalingType_RoundTripsThroughWireToken(WebRtcSignalingType type, string expected)
    {
        type.ToWireName().ShouldBe(expected);
        WebRtcSignalingTypeExtensions.Parse(expected).ShouldBe(type);
    }

    [Theory]
    [InlineData("OFFER", WebRtcSignalingType.Offer)] // case-insensitive
    [InlineData("nonsense", null)]
    [InlineData("", null)]
    public void SignalingType_ParsesTolerantly(string value, WebRtcSignalingType? expected)
    {
        WebRtcSignalingTypeExtensions.Parse(value).ShouldBe(expected);
    }

    [Fact]
    public void ServerBinding_SerializesSignalingAndIceServers()
    {
        var binding = new WebRtcServerBinding
        {
            SignalingUrl = "wss://signal.example.com",
            IceServers = { "stun:stun.l.google.com:19302", "turn:turn.example.com" },
            BundlePolicy = WebRtcProtocol.BundlePolicies.MaxBundle,
        };

        var json = SerializeV3(binding);

        json["signalingUrl"]!.GetValue<string>().ShouldBe("wss://signal.example.com");
        json["iceServers"]!.AsArray().Select(n => n!.GetValue<string>())
            .ShouldBe(new[] { "stun:stun.l.google.com:19302", "turn:turn.example.com" });
        json["bundlePolicy"]!.GetValue<string>().ShouldBe("max-bundle");
    }

    [Fact]
    public void ExplicitBindingVersion_IsHonored()
    {
        var binding = new WebRtcChannelBinding { Label = "chat", BindingVersion = "1.2.3" };

        SerializeV3(binding)["bindingVersion"]!.GetValue<string>().ShouldBe("1.2.3");
    }

    [Fact]
    public void V2AndV3_ProduceEquivalentOperationOutput()
    {
        var binding = new WebRtcOperationBinding { SignalingType = WebRtcSignalingType.Answer };

        SerializeV2(binding).ToJsonString().ShouldBe(SerializeV3(binding).ToJsonString());
    }
}
