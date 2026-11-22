---
"bielu-aspnetcore-asyncapi": patch
---

`Bielu.AspNetCore.AsyncApi.Wolverine` now supports WolverineFx 7 as well as WolverineFx 6. The package still depends on WolverineFx 6.41.0 or newer, and CI tests the same package on 6.41.0 and 7.0.0-alpha.3 for `net10.0` and `net11.0`. A listener's reply now comes from the handlers Wolverine runs for that listener. If a handler is bound to the listener (a sticky handler), its returned messages are used. Otherwise the handlers bound to no endpoint are used. Before, two handlers bound to different listeners for the same message gave no reply on Wolverine 6, and on Wolverine 7 they threw `AmbiguousHandlerChainException` when the document was generated. A listener that only relays its message to handlers on local queues has no reply.
