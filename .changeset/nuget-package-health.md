---
"bielu-aspnetcore-asyncapi": patch
---

The NuGet packages now pass NuGet Package Explorer's health checks (Source Link, deterministic, compiler flags). Each package ships a `.snupkg` symbol package to the nuget.org symbol server, with Source Link pointing at the GitHub commit, and CI builds set `ContinuousIntegrationBuild` so the build is deterministic.
