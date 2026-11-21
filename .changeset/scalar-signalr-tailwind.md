---
"@bielu/scalar-signalr": patch
"bielu-aspnetcore-asyncapi": patch
---

The SignalR console in Scalar is styled with Tailwind CSS on Scalar's own theme, like the broker console, so its colours, type scale and spacing come from the active Scalar theme. Sample payloads under "Events" get the grey left border they were meant to have; a later rule had made them green, like a successful call's result. The cards, inputs, buttons, method tags, pills, results and message log it shares with the gRPC console are now `bielu-*` utilities in `@bielu/scalar-core`.
