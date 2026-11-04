---
"bielu-aspnetcore-asyncapi": minor
"@bielu/scalar-signalr": patch
"@bielu/scalar-grpc": patch
---

Write custom protocol bindings as `x-` extensions, so generated AsyncAPI 3 documents validate against the AsyncAPI JSON Schema. AsyncAPI 3 bindings objects only allow the specification's binding names (`amqp`, `http`, `kafka`, `ws`, ...) and `x-` extensions, so the `signalr`, `grpc`, `webrtc` and `sse` bindings are now written under `x-signalr`, `x-grpc`, `x-webrtc` and `x-sse`. The new `SignalRProtocol.BindingKey`, `GrpcProtocol.BindingKey`, `WebRtcProtocol.BindingKey` and `SseProtocol.BindingKey` constants hold the key; `ProtocolName` is unchanged and is still a server's `protocol`.

**Changes the generated document:** a consumer that reads `bindings.signalr` (or `grpc`, `webrtc`, `sse`) needs to read `bindings["x-signalr"]` instead. The Scalar SignalR and gRPC consoles read either key, the bare name first, so they work with documents from before and after this change, and the MCP operation search reports `x-signalr` as the `signalr` protocol.
