---
"bielu-aspnetcore-asyncapi": minor
---

Added `Bielu.AspNetCore.AsyncApi.Scalar.Broker` and the `@bielu/scalar-broker` npm bundle: an
interactive message-broker console for the Scalar API Reference, mirroring the SignalR and gRPC
consoles on the shared `Bielu.AspNetCore.AsyncApi.Scalar` / `@bielu/scalar-core` foundation. The
console reads `kafka`, `mqtt` and `amqp` bindings out of your AsyncAPI document(s), prefills a
publish editor from the channel's message payload schema, and tails the channel live.

A browser cannot speak any of these protocols, so — unlike the gRPC console, which reaches the
server directly over gRPC-Web — this console goes through a server-side bridge that
`MapScalarBrokerAssets()` mounts alongside the bundle: `GET {path}/connections`,
`POST {path}/publish`, and `GET {path}/tail` (Server-Sent Events). Driver packages implement
`IBrokerBridge` and register connections through `AddScalarBrokerBridge(o => o.Add…Connection(…))`;
bridges are built lazily, so a broker that is unreachable at startup cannot stop the app from
starting.

**The bridge can publish to your broker, so it is not open by default.** Nothing is exposed until
`MapScalarBrokerAssets()` is called, and that call returns a convention builder covering all three
proxy endpoints so one `RequireAuthorization(...)` protects them together. Without authorization
metadata the proxy allows requests only in the Development environment (logging a warning once) and
answers `403` anywhere else, unless the operator explicitly sets `AllowAnonymous`. The bundle itself
stays unauthenticated: it is static JavaScript holding no configuration or secrets, and it is inert
without the proxy behind it.

Two consequences of that shape worth recording. Tail subscriptions are ephemeral and start at the
newest offset, so opening a console never disturbs a real consumer group's committed position and
never replays a backlog into the browser. And the tail stream is read with `fetch` +
`ReadableStream` rather than `EventSource`, because `EventSource` cannot send the `Authorization`
header the proxy sits behind — which also lets Scalar's auth panel drive the proxy, including API
keys in a query parameter, which the gRPC console has to downgrade to a header.

`Bielu.AspNetCore.AsyncApi.Scalar.Broker.Kafka` is the first driver, and the reference for the next
ones. Tailing a topic uses a throwaway consumer group (`bielu-scalar-broker-{guid}`) starting at
`AutoOffsetReset.Latest` with auto-commit off, so opening a console cannot move a real consumer
group's committed offsets or replay a backlog into someone's browser. `Confluent.Kafka` is pinned to
`2.14.0`, the version `Aspire.Confluent.Kafka` 13.4.6 already depends on, so the Aspire mini-shop
resolves one copy rather than two. The connection descriptor sent to the browser carries a redacted
endpoint only — any `user:password@` in `bootstrapServers` is stripped, and a plain `host:port` is
left alone.

Wired into the Aspire mini-shop's Order Service, which already publishes order events to Kafka and
already declares `kafka` channel bindings.

This package ships no `dist/scalar-plugin.mjs` and no standalone bundle, and there is no `.Aspire`
companion. All three exist to load a console into the Scalar *container* from a CDN, and a
CDN-hosted broker plugin would have no server-side bridge to talk to — the broker console has to be
mapped in the app that owns the broker connection.
