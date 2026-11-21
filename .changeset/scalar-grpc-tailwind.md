---
"@bielu/scalar-grpc": patch
"bielu-aspnetcore-asyncapi": patch
---

The gRPC console in Scalar is styled with Tailwind CSS on Scalar's own theme, using the `bielu-*` utilities it shares with the SignalR console, so its colours, type scale and spacing come from the active Scalar theme. The sample request shown for a method that can't be called from the browser gets the grey left border it was meant to have; a later rule had made it green, like a successful call's result.
