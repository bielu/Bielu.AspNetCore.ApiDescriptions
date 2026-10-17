---
"bielu-aspnetcore-asyncapi": patch
---

Fix merged documents losing every protocol binding. The merger, and the CLI's `validate` and `diff` commands, read documents without any binding parsers registered, so all server, channel, operation and message bindings were silently dropped. They now register ByteBard's standard bindings (Kafka, AMQP, MQTT, HTTP, WebSockets, SNS, SQS, Pulsar). Unused internal code left over from the `Microsoft.AspNetCore.OpenApi` port is also removed.
