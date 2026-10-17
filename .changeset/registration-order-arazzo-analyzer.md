---
"bielu-aspnetcore-asyncapi": minor
---

`Bielu.AspNetCore.Arazzo` now ships its own Roslyn analyzers, with `BARAZZO` rule ids, which check workflow-builder code at compile time:

- **BARAZZO001** (warning): `AddOpenApi()` is called after `AddArazzo()` in the same method, so build-time generation may miss the Arazzo documents.
- **BARAZZO002** (error): a literal workflow id, step id or source name, or a marker type's derived id, isn't a valid Arazzo identifier. The builders throw this at startup.
- **BARAZZO003** (error): a step sets no target, or more than one, of `Operation`/`OperationPath`/`Channel`/`Workflow`.
- **BARAZZO004** (error): a `Channel` step's action is neither `"send"` nor `"receive"`.
- **BARAZZO005** (warning): `WithCorrelationId` is set on a step that doesn't receive.
- **BARAZZO006** (warning): a step names a source description the document never declares.
- **BARAZZO007** (warning): `DependsOn` or `Workflow` names a step or workflow the document never defines. Nothing checked this before, not even at runtime.
- **BARAZZO008** (info): `AddAsyncApiSource`/`AddOpenApiSource` points at a document the project never registers.

Existing code that sets an invalid id, two step targets or an unknown action already fails at startup, and now fails to compile instead. The build-time generation docs also state exactly which registration orders affect which tools.
