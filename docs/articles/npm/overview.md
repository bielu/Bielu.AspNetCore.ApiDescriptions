# NPM packages

The Scalar plugins behind the .NET packages are published to npm too. Use them from a JavaScript app,
or from any static page, with the [Scalar API Reference](https://github.com/scalar/scalar/tree/main/packages/api-reference)
and no ASP.NET Core app.

| Package | What it adds to Scalar | Setup |
|---------|------------------------|-------|
| [`@bielu/scalar-arazzo`](https://www.npmjs.com/package/@bielu/scalar-arazzo) | An Arazzo document type: workflows and steps in the sidebar, a flowchart of each workflow, and links from each step to the OpenAPI operation or AsyncAPI channel it calls. Static documents are enough. | [Arazzo for Scalar](scalar-arazzo.md) |
| [`@bielu/scalar-signalr`](https://www.npmjs.com/package/@bielu/scalar-signalr) | A live SignalR console: connect to hubs, invoke methods, watch server events. Needs an AsyncAPI document with SignalR bindings and a reachable hub. | [SignalR console](../scalar-consoles.md#signalr-console) |
| [`@bielu/scalar-grpc`](https://www.npmjs.com/package/@bielu/scalar-grpc) | A live gRPC console for unary and server-streaming methods over gRPC-Web. Needs a server with gRPC-Web enabled and the protobuf descriptor endpoint the .NET package maps. | [gRPC console](../scalar-consoles.md#grpc-console) |
| [`@bielu/scalar-broker`](https://www.npmjs.com/package/@bielu/scalar-broker) | A Kafka/MQTT/AMQP console that publishes to and tails channels. No browser speaks these protocols, so it needs the server-side bridge `Bielu.AspNetCore.AsyncApi.Scalar.Broker` mounts in an ASP.NET Core app. | [Broker console](../scalar-consoles.md#broker-console-kafka) |

All four need `@scalar/api-reference`. The Arazzo plugin needs 1.80.0 or later, the first release
with plugin `documentTypes`.

Each package ships the same plugin three ways:

- **An ES module** to import and pass to `createApiReference({ plugins: [...] })`.
- **A `<script>` bundle** that adds itself to every `Scalar.createApiReference` call on the page.
- **`dist/scalar-plugin.mjs`**, whose default export is the plugin, for Scalar's `pluginUrls` option.
  This is how the .NET Aspire packages add the plugins to the Scalar container.
