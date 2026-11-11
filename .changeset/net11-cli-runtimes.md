---
"bielu-aspnetcore-asyncapi": minor
---

`dotnet asyncapi`, `dotnet arazzo` and `dotnet overlay` ship `net10.0` and `net11.0` builds. `dotnet asyncapi
getdocument` generates each document on the runtime the application targets: when that differs from the runtime
the CLI is on, it relaunches itself with the app's `runtimeconfig.json`/`deps.json`, as `dotnet getdocument` does,
so a net11.0 app no longer crashes a CLI on .NET 10 (`Could not load file or assembly 'System.Runtime,
Version=11.0.0.0'`). Arguments, output and exit codes are unchanged, and an app on the CLI's own runtime runs
in-process exactly as before.

The `Bielu.AspNetCore.AsyncApi.ApiDescription.Server` MSBuild target runs once per target framework (never in the
outer or design-time build). In a multi-targeting project with a custom `AsyncApiDocumentsDirectory`, each framework
now writes to its own `<TargetFramework>` subfolder (opt out with `AsyncApiDocumentsPerTargetFramework=false`). It
also passes absolute `--assembly-path`/`--file-list` paths: the relative ones it used were resolved against the tool's
working directory, `$(OutputPath)`, and pointed one `OutputPath` too deep.
