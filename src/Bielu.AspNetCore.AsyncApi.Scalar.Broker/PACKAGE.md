# Bielu.AspNetCore.AsyncApi.Scalar.Broker

Adds an interactive **message-broker console** to the [Scalar](https://scalar.com) API Reference in
ASP.NET Core. The console reads the `kafka`, `mqtt` and `amqp` bindings out of your AsyncAPI
document(s) and lets you publish to a channel and tail it live, next to the rest of your API
documentation.

A browser cannot speak Kafka, MQTT or AMQP, so the console reaches your broker through an **opt-in
server-side bridge** that this package mounts in your app.

> **This bridge can publish to your broker.** Nothing is exposed until you call
> `MapScalarBrokerAssets()`, and outside the Development environment the proxy endpoints refuse
> unauthenticated callers unless you say otherwise. Read [Securing the bridge](#securing-the-bridge)
> before deploying it.

## Install

This package contains the console and the bridge abstraction; add a driver for the broker you use:

```shell
dotnet add package Bielu.AspNetCore.AsyncApi.Scalar.Broker
dotnet add package Bielu.AspNetCore.AsyncApi.Scalar.Broker.Kafka
```

## Usage

```csharp
builder.Services.AddScalarBrokerBridge(options =>
{
    options.AddKafkaConnection("orders", "localhost:9092");
});

app.MapAsyncApi();

// Serves the console bundle and the publish/tail proxy endpoints.
app.MapScalarBrokerAssets()
   .RequireAuthorization("BrokerConsole");

app.MapScalarApiReference(options =>
{
    options.AddAsyncApiDocument("v1", "Orders", "/asyncapi/v1.json");
    options.WithBrokerClient();
});
```

`MapScalarBrokerAssets()` mounts four endpoints under `/bielu/scalar/broker` (configurable):

| Endpoint | Purpose |
| --- | --- |
| `GET {path}/plugin.js` | The console bundle (the `@bielu/scalar-broker` build). |
| `GET {path}/connections` | The registered connections — name, protocol, and a credential-free endpoint label. |
| `POST {path}/publish` | Publishes one message to a channel. |
| `GET {path}/tail` | Server-Sent Events stream of messages consumed from a channel. |

## Securing the bridge

The returned builder covers the three proxy endpoints, so a single `RequireAuthorization(...)` on
the result of `MapScalarBrokerAssets()` protects all of them. The bundle itself is not covered: it
is static JavaScript with no configuration or secrets in it, and it is useless without the proxy.

If the endpoints carry no authorization metadata, the proxy:

- **allows** the request in the Development environment, logging a warning once;
- **refuses** it with `403` anywhere else, logging how to fix it.

Set `AllowAnonymous = true` on `AddScalarBrokerBridge` only when the endpoints are protected by
something ASP.NET Core authorization cannot see, such as a network boundary.

Credentials entered in Scalar's auth panel travel with every proxy call — bearer tokens, HTTP Basic,
and API keys in a header or query parameter.

## Tailing

Tail subscriptions are **ephemeral and start at the newest offset**: opening a console never
disturbs a real consumer group's committed position and never replays a backlog into the browser.
The stream is read with `fetch` + `ReadableStream` rather than `EventSource`, because `EventSource`
cannot send the `Authorization` header the proxy sits behind.

## Writing a driver

Implement `IBrokerBridge` (`PublishAsync` + `TailAsync`) and register it with an extension method on
`ScalarBrokerBridgeOptions` that calls `AddConnection`. See
`Bielu.AspNetCore.AsyncApi.Scalar.Broker.Kafka` for a worked example.

## Documentation

Full documentation: <https://apidescriptions.bielu.pl/>
