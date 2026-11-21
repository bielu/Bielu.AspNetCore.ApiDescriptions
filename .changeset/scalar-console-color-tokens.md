---
"@bielu/scalar-broker": patch
"@bielu/scalar-signalr": patch
"bielu-aspnetcore-asyncapi": patch
---

The broker console's log entries show their direction marker again: a green bar for received messages, blue for published and red for system notes. A more specific rule had made every marker transparent. The markers, the broker console's error text and the SignalR console's "connected" pill now use Scalar's `--scalar-color-*` variables instead of fixed hex colours, so they follow the active Scalar theme and dark mode.
