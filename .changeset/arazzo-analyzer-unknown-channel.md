---
"bielu-aspnetcore-asyncapi": minor
---

New analyzer BARAZZO009 warns when a `Channel(...)` step into a self-wired AsyncAPI source (`AddAsyncApiSource`) names a channel that no `[Channel]` attribute in the project or its references declares. A renamed or removed channel now fails the build rather than startup. Channel keys follow the AsyncAPI generator, so `[Channel("orders/{id}")]` is keyed `ordersid`, and a step written with the declared name is told which key to use. For a channel created at runtime, such as by Wolverine routing or a document transformer, the new `ArazzoStepBuilder.Channel(sourceName, channelName, action, dynamic)` overload with `dynamic: true` tells the analyzer to skip the step. The generated step is the same either way. The rule also stays quiet in projects that call `AddWolverine()`.
