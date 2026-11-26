---
"bielu-aspnetcore-asyncapi": patch
---

`dotnet asyncapi getdocument` now reports "--assembly requires a value." (and the same for its other options) and exits with code 1 when an option is the last argument, instead of crashing with an `IndexOutOfRangeException`.
