---
"@bielu/scalar-broker": patch
"@bielu/scalar-signalr": patch
"bielu-aspnetcore-asyncapi": patch
---

The broker console's error status and log markers, and the SignalR console's "connected" pill, use Scalar's `--scalar-color-red`/`-green`/`-blue` tokens instead of fixed hex colours, so they follow the active Scalar theme and dark mode.
