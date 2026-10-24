using System.Text.Json.Nodes;
using ByteBard.AsyncAPI.Bindings;
using ByteBard.AsyncAPI.Models;
using ByteBard.AsyncAPI.Readers;
using ByteBard.AsyncAPI.Writers;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Extensions.Protocols.WebRtc.Tests;

/// <summary>
/// Reads every WebRTC binding (server, channel, operation, message) from a document with all fields set, then
/// writes each back out: the bindings must parse into their typed form and serialize to exactly what was read.
/// </summary>
public class WebRtcBindingReadRoundTripTests
{
    private const string Document = """
        {
          "asyncapi": "3.0.0",
          "info": { "title": "WebRTC", "version": "1.0.0" },
          "servers": {
            "signaling": {
              "host": "rtc.example.com",
              "protocol": "webrtc",
              "bindings": {
                "webrtc": {
                  "signalingUrl": "wss://rtc.example.com/signal",
                  "iceServers": ["stun:stun.example.com:3478"],
                  "bundlePolicy": "max-bundle",
                  "bindingVersion": "0.1.0"
                }
              }
            }
          },
          "channels": {
            "telemetry": {
              "address": "telemetry",
              "bindings": {
                "webrtc": {
                  "channelType": "dataChannel",
                  "label": "telemetry",
                  "subProtocol": "json",
                  "ordered": false,
                  "maxRetransmits": 3,
                  "maxPacketLifeTime": 500,
                  "negotiated": true,
                  "id": 7,
                  "bindingVersion": "0.1.0"
                }
              },
              "messages": {
                "offer": {
                  "payload": { "type": "object" },
                  "bindings": {
                    "webrtc": { "signalingType": "offer", "encoding": "sdp", "headers": { "type": "object" }, "bindingVersion": "0.1.0" }
                  }
                }
              }
            }
          },
          "operations": {
            "negotiate": {
              "action": "send",
              "channel": { "$ref": "#/channels/telemetry" },
              "bindings": { "webrtc": { "signalingType": "candidate", "direction": "bidirectional", "bindingVersion": "0.1.0" } }
            }
          }
        }
        """;

    private static AsyncApiDocument Read(string json = Document)
    {
        var settings = new AsyncApiReaderSettings();
        settings.Bindings.Add(new WebRtcServerBinding());
        settings.Bindings.Add(new WebRtcChannelBinding());
        settings.Bindings.Add(new WebRtcOperationBinding());
        settings.Bindings.Add(new WebRtcMessageBinding());

        return new AsyncApiStringReader(settings).Read(json, out _);
    }

    private static JsonNode Write(AsyncApiBinding binding, bool v2)
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

        return JsonNode.Parse(stringWriter.ToString())!;
    }

    private static void ShouldRoundTrip(AsyncApiBinding binding, string pointer)
    {
        var expected = JsonNode.Parse(Document)!;
        foreach (var segment in pointer.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            expected = expected[segment]!;
        }

        JsonNode.DeepEquals(Write(binding, v2: false), expected).ShouldBeTrue($"V3 {pointer}: {Write(binding, v2: false).ToJsonString()}");
        JsonNode.DeepEquals(Write(binding, v2: true), expected).ShouldBeTrue($"V2 {pointer}: {Write(binding, v2: true).ToJsonString()}");
    }

    [Fact]
    public void Read_AllBindings_ParseEveryField()
    {
        // Act
        var document = Read();

        // Assert
        var server = document.Servers["signaling"].Bindings.Values.OfType<WebRtcServerBinding>().ShouldHaveSingleItem();
        server.SignalingUrl.ShouldBe("wss://rtc.example.com/signal");
        server.IceServers.ShouldBe(["stun:stun.example.com:3478"]);
        server.BundlePolicy.ShouldBe("max-bundle");

        var channel = document.Channels["telemetry"].Bindings.Values.OfType<WebRtcChannelBinding>().ShouldHaveSingleItem();
        channel.Label.ShouldBe("telemetry");
        channel.Ordered.ShouldBe(false);
        channel.MaxRetransmits.ShouldBe(3);
        channel.MaxPacketLifeTime.ShouldBe(500);
        channel.Negotiated.ShouldBe(true);
        channel.Id.ShouldBe(7);

        var operation = document.Operations["negotiate"].Bindings.Values.OfType<WebRtcOperationBinding>().ShouldHaveSingleItem();
        operation.SignalingType.ShouldBe(WebRtcSignalingType.Candidate);

        var message = document.Channels["telemetry"].Messages["offer"].Bindings.Values.OfType<WebRtcMessageBinding>().ShouldHaveSingleItem();
        message.SignalingType.ShouldBe(WebRtcSignalingType.Offer);
        message.Headers.ShouldNotBeNull();
    }

    [Fact]
    public void Write_ReadBindings_ReproduceTheSourceJson()
    {
        // Arrange
        var document = Read();

        // Act & Assert
        ShouldRoundTrip(document.Servers["signaling"].Bindings.Values.OfType<AsyncApiBinding>().Single(), "/servers/signaling/bindings/webrtc");
        ShouldRoundTrip(document.Channels["telemetry"].Bindings.Values.OfType<AsyncApiBinding>().Single(), "/channels/telemetry/bindings/webrtc");
        ShouldRoundTrip(document.Operations["negotiate"].Bindings.Values.OfType<AsyncApiBinding>().Single(), "/operations/negotiate/bindings/webrtc");
        ShouldRoundTrip(document.Channels["telemetry"].Messages["offer"].Bindings.Values.OfType<AsyncApiBinding>().Single(), "/channels/telemetry/messages/offer/bindings/webrtc");
    }

    [Fact]
    public void Read_NonNumericLimits_LeavesThemUnset()
    {
        // Act
        var document = Read(Document.Replace("\"maxRetransmits\": 3", "\"maxRetransmits\": \"many\""));

        // Assert
        document.Channels["telemetry"].Bindings.Values.OfType<WebRtcChannelBinding>().ShouldHaveSingleItem()
            .MaxRetransmits.ShouldBeNull();
    }
}
