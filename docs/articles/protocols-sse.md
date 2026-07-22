# Server-Sent Events (SSE) Protocol

The `Bielu.AspNetCore.AsyncApi.Extensions.Protocols.Sse` package adds support for documenting SSE endpoints.

## Installation

```bash
dotnet add package Bielu.AspNetCore.AsyncApi.Extensions.Protocols.Sse
```

## Configuration

```csharp
builder.Services.AddAsyncApi(options =>
{
    options.AddSseChannelBinding("events", b =>
    {
        b.Path = "/events";
        b.Method = SseProtocol.Methods.Get;
    });

    options.AddSseOperationBinding("onPriceUpdate", b =>
    {
        b.Direction = SseProtocol.Directions.ServerToClient;
    });
});
```

## Bindings Reference

| Binding | Notable fields |
| --- | --- |
| `SseChannelBinding` | `Path`, `Method`, `ContentType` (defaults to `text/event-stream`), `Query`, `Headers` |
| `SseOperationBinding` | `Method`, `Direction` (defaults to `serverToClient`) |
| `SseMessageBinding` | `Event` (the `event:` field), `Id` (`id:`), `Retry` |
| `SseServerBinding` | `Retry`, `Heartbeat` |
