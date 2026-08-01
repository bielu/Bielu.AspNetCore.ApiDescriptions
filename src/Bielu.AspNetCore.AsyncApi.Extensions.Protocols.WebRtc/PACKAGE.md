# Bielu.AspNetCore.AsyncApi.Extensions.Protocols.WebRtc

WebRTC protocol bindings for
[Bielu.AspNetCore.AsyncApi](https://www.nuget.org/packages/Bielu.AspNetCore.AsyncApi).

AsyncAPI has no built-in `webrtc` protocol, so this package adds one: channel, operation,
message and server binding types that describe data channels, their negotiation parameters and the signaling exchange, serialized into the generated document
under the `webrtc` binding key.

## Installation

```sh
dotnet add package Bielu.AspNetCore.AsyncApi.Extensions.Protocols.WebRtc
```

## Usage

Register a server using the protocol, then attach bindings to the channels and operations that use
it:

```csharp
builder.Services.AddAsyncApi(options =>
{
    options.AddServer("webrtc", "localhost:5000", WebRtcProtocol.ProtocolName);
});
```

Because the bindings are ordinary AsyncAPI binding objects, any AsyncAPI-aware tool reads the
document — and the matching interactive console can drive the protocol from a browser.

## Documentation

- [WebRTC bindings](https://apidescriptions.bielu.pl/articles/protocols-webrtc.html)
- [Full documentation](https://apidescriptions.bielu.pl/)

## Feedback & Contributing

Released under the [MIT license](https://licenses.nuget.org/MIT). Bug reports and contributions are
welcome at [the GitHub repository](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions).
