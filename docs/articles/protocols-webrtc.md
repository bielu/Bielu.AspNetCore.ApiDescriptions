# WebRTC Protocol

The `Bielu.AspNetCore.AsyncApi.Extensions.Protocols.WebRtc` package adds support for documenting WebRTC signaling and data channels.

## Installation

```bash
dotnet add package Bielu.AspNetCore.AsyncApi.Extensions.Protocols.WebRtc
```

## Configuration

```csharp
builder.Services.AddAsyncApi(options =>
{
    options.AddServer("webrtc", "signal.example.com", "webrtc");

    options.AddWebRtcChannelBinding("chat", b =>
    {
        b.ChannelType = WebRtcProtocol.ChannelTypes.DataChannel;
        b.Label = "chat";
        b.Ordered = true;
    });

    options.AddWebRtcOperationBinding("sendOffer", b =>
    {
        b.SignalingType = WebRtcSignalingType.Offer;
        b.Direction = WebRtcProtocol.Directions.ClientToServer;
    });
});
```

## Bindings Reference

| Binding | Notable fields |
| --- | --- |
| `WebRtcChannelBinding` | `ChannelType` (`dataChannel`/`media`), `Label`, `SubProtocol`, `Ordered`, `MaxRetransmits`, `MaxPacketLifeTime`, `Negotiated`, `Id` |
| `WebRtcOperationBinding` | `SignalingType` (`offer`/`answer`/`candidate`), `Direction` |
| `WebRtcMessageBinding` | `SignalingType`, `Encoding` (`text`/`binary`/`json`), `Headers` |
| `WebRtcServerBinding` | `SignalingUrl`, `IceServers` (STUN/TURN URLs), `BundlePolicy` |

## Signaling Types

| Token | Meaning |
| --- | --- |
| `offer` | SDP offer initiating negotiation |
| `answer` | SDP answer responding to an offer |
| `candidate` | An ICE candidate discovered during connectivity checks |
