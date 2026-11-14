---
"bielu-aspnetcore-asyncapi": minor
---

Add `ScalarBrokerBridgeOptions.AllowChannels(...)` to limit the channels the Scalar broker console's proxy may publish to
and tail. Requests for any other channel are refused with `403`. Authorization decides who may use the proxy; without an
allow-list, an authorized caller can reach any topic the connection's credentials allow, not only the documented
channels. Without `AllowChannels`, every channel is allowed, as before.
