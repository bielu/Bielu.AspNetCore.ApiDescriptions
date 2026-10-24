---
"bielu-aspnetcore-asyncapi": patch
---

The broker-bindings docs no longer list the three ByteBard 3.0.1 binding bugs (WebSockets key, Kafka `schemaIdLocation` casing, SQS `fifoQueue`) as known issues, since ByteBard 4.0 fixes them. Tests now validate all three against the official AsyncAPI schema.
