using System.Text.Json.Nodes;
using Bielu.AspNetCore.AsyncApi.Extensions.Protocols.WebRtc;
using ByteBard.AsyncAPI.Models;
using ByteBard.AsyncAPI.Models.Interfaces;
using ByteBard.AsyncAPI.Writers;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Extensions.Protocols.WebRtc.Tests;

/// <summary>
/// End-to-end example: builds a complete AsyncAPI document describing a WebRTC peer connection with a
/// chat data channel, using all four WebRTC bindings (server, channel, operation, message) and
/// serializes it to AsyncAPI v3 JSON.
/// </summary>
public class WebRtcDocumentExampleTests
{
    private static AsyncApiDocument BuildChatDocument()
    {
        var server = new AsyncApiServer
        {
            Host = "signal.example.com",
            Protocol = WebRtcProtocol.ProtocolName,
            Bindings = new AsyncApiBindings<IServerBinding>
            {
                new WebRtcServerBinding
                {
                    SignalingUrl = "wss://signal.example.com",
                    IceServers = { "stun:stun.l.google.com:19302" },
                    BundlePolicy = WebRtcProtocol.BundlePolicies.MaxBundle,
                },
            },
        };

        var offerMessage = new AsyncApiMessage
        {
            Name = "Offer",
            Bindings = new AsyncApiBindings<IMessageBinding>
            {
                new WebRtcMessageBinding { SignalingType = WebRtcSignalingType.Offer, Encoding = WebRtcProtocol.Encodings.Json },
            },
        };

        var channel = new AsyncApiChannel
        {
            Address = "chat",
            Messages = { ["offer"] = offerMessage },
            Bindings = new AsyncApiBindings<IChannelBinding>
            {
                new WebRtcChannelBinding
                {
                    ChannelType = WebRtcProtocol.ChannelTypes.DataChannel,
                    Label = "chat",
                    Ordered = true,
                },
            },
        };

        var operation = new AsyncApiOperation
        {
            Action = AsyncApiAction.Send,
            Channel = new AsyncApiChannelReference("#/channels/chat"),
            Bindings = new AsyncApiBindings<IOperationBinding>
            {
                new WebRtcOperationBinding
                {
                    SignalingType = WebRtcSignalingType.Offer,
                    Direction = WebRtcProtocol.Directions.ClientToServer,
                },
            },
        };

        var document = new AsyncApiDocument
        {
            Info = new AsyncApiInfo { Title = "WebRTC Chat", Version = "1.0.0" },
        };
        document.Servers["webrtc"] = server;
        document.Channels["chat"] = channel;
        document.Operations["sendOffer"] = operation;
        return document;
    }

    [Fact]
    public void ChatDocument_SerializesWithWebRtcBindings()
    {
        var document = BuildChatDocument();

        using var stringWriter = new StringWriter();
        document.SerializeV3(new AsyncApiJsonWriter(stringWriter));
        stringWriter.Flush();
        var json = JsonNode.Parse(stringWriter.ToString())!;

        json["servers"]!["webrtc"]!["bindings"]!["webrtc"]!["signalingUrl"]!.GetValue<string>()
            .ShouldBe("wss://signal.example.com");
        json["channels"]!["chat"]!["bindings"]!["webrtc"]!["label"]!.GetValue<string>().ShouldBe("chat");
        json["operations"]!["sendOffer"]!["bindings"]!["webrtc"]!["signalingType"]!.GetValue<string>()
            .ShouldBe("offer");
        json["channels"]!["chat"]!["messages"]!["offer"]!["bindings"]!["webrtc"]!["signalingType"]!
            .GetValue<string>().ShouldBe("offer");
    }
}
