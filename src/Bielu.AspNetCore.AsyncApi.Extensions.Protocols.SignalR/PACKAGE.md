# Bielu.AspNetCore.AsyncApi.Extensions.Protocols.SignalR

SignalR protocol bindings for
[Bielu.AspNetCore.AsyncApi](https://www.nuget.org/packages/Bielu.AspNetCore.AsyncApi).

AsyncAPI has no built-in `signalr` protocol, so this package adds one: channel, operation,
message and server binding types that describe hubs, hub methods and the client callbacks they invoke, serialized into the generated document
under the `signalr` binding key.

## Installation

```sh
dotnet add package Bielu.AspNetCore.AsyncApi.Extensions.Protocols.SignalR
```

## Usage

Register a server using the protocol, then attach bindings to the channels and operations that use
it:

```csharp
builder.Services.AddAsyncApi(options =>
{
    options.AddServer("signalr", "localhost:5000", SignalRProtocol.ProtocolName);
});
```

Because the bindings are ordinary AsyncAPI binding objects, any AsyncAPI-aware tool reads the
document — and the matching interactive console can drive the protocol from a browser.

## Documentation

- [SignalR bindings](https://apidescriptions.bielu.pl/articles/protocols-signalr.html)
- [Full documentation](https://apidescriptions.bielu.pl/)

## Feedback & Contributing

Released under the [MIT license](https://licenses.nuget.org/MIT). Bug reports and contributions are
welcome at [the GitHub repository](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions).
