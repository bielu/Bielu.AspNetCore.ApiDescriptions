---
"bielu-aspnetcore-asyncapi": patch
---

`dotnet asyncapi getdocument` no longer crashes with an `IndexOutOfRangeException` when an option is given without a value (for example a trailing `--output`). It now reports "`--output` requires a value." and exits with code 1, like the `merge`, `validate` and `diff` commands.
