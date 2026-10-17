---
"bielu-aspnetcore-asyncapi": patch
---

The `validate` commands of `dotnet asyncapi`, `dotnet arazzo` and `dotnet overlay` no longer crash with a `DirectoryNotFoundException` when a `--file` glob points into a directory that does not exist. The glob matches nothing, and the command reports that no files were found.
