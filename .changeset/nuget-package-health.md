---
"bielu-aspnetcore-asyncapi": patch
---

The NuGet packages now pass NuGet Package Explorer's health checks (Source Link, deterministic, compiler flags). Assemblies embed their PDB, so symbols and Source Link work without a symbol package, and CI builds set `ContinuousIntegrationBuild` so the build is deterministic.
