---
"bielu-aspnetcore-asyncapi": minor
---

`ArazzoOptions` lines up with `AsyncApiOptions`. A document configured without `WithInfo` no longer throws "has no Info" when it is built: its `info` defaults as an AsyncAPI document's does, to the title `"{ApplicationName} | {documentName}"` and version `1.0.0` (`ArazzoDefaults.DefaultInfoVersion`). `WithDescription` sets the `info` description.
