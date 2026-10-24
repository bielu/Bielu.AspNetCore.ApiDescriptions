---
"bielu-aspnetcore-asyncapi": minor
---

Add `Bielu.AspNetCore.AsyncApi.CloudEvents`, a new package that describes a document's messages as CloudEvents 1.0 events. `AsyncApiOptions.AddCloudEvents()` adds the required `id`, `source`, `specversion` and `type` attributes and the optional `dataschema`, `subject` and `time` to each message's headers (binary mode, the default). The header names follow the message's protocol binding, chosen from the protocol of its channels' servers: `ce-` for HTTP, `ce_` for Kafka, `cloudEvents:` for AMQP, and no prefix for MQTT 5. Existing headers are combined with `allOf`. `UseMode(CloudEventsMode.Structured)` instead wraps the payload in the `application/cloudevents+json` envelope. The event type (`WithTypePrefix`, defaulting to the message name) and source (`WithSource`) are pinned with `const` and can be overridden per message (`ConfigureMessage`). Message examples gain the attributes too.
