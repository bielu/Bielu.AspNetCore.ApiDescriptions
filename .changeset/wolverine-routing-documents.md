---
"bielu-aspnetcore-asyncapi": minor
---

New `Bielu.AspNetCore.AsyncApi.Wolverine` package. `options.AddWolverine(...)` builds channels, operations and messages from Wolverine's message routing: each route to an external transport (Kafka, SignalR, ...) becomes a `send` operation, each listener with a default incoming message type a `receive` operation, and messages are named by Wolverine's message type name (`[MessageIdentity]`), so the document uses the same names as the wire. SignalR channels resolve to the path the `WolverineHub` is mapped at and carry `signalr` bindings targeting `ReceiveMessage`; Kafka channels carry a `kafka` topic binding.
