---
"bielu-aspnetcore-asyncapi": minor
---

Added `Bielu.Overlay.NET` and `Bielu.Overlay.NET.Readers`, a framework-free object model, reader, validator, and apply engine for the [OpenAPI Overlay Specification](https://spec.openapis.org/overlay/latest.html) — the OAI's companion spec for declarative, repeatable transformations of an API description. Supports both 1.0.0 and 1.1.0 with version-gated semantics: 1.1.0 added the `copy` action, pinned `target` to RFC 9535 JSONPath, and legalized primitive targets and array concatenation, so a document declaring `1.0.0` gets 1.0.0 behaviour and using a 1.1.0-only feature there is reported rather than silently applied.

The engine operates on `System.Text.Json.Nodes.JsonNode` and nothing else. The Overlay Specification is written against OpenAPI, but its mechanism — select nodes by JSONPath, then merge/copy/remove — carries no OpenAPI-specific assumptions, so **overlays here apply to AsyncAPI and Arazzo documents as readily as to OpenAPI ones**. That is not something an implementation bound to a particular object model can do, and it is the reason this exists in a repository that generates AsyncAPI.

Worth knowing when writing an Arazzo overlay: Arazzo keys `workflows`, `steps`, and `sourceDescriptions` as **arrays of objects carrying an id field**, where OpenAPI and AsyncAPI use maps. There is no `$.workflows.measureAndAlert` to target, so every Arazzo target is a filter expression (`$.workflows[?@.workflowId == 'measureAndAlert']`) and every removal deletes an array element rather than a map key. Both styles are covered by tests and shown side by side in the example. `OverlayApplier.Apply` never mutates its input, so one overlay can be applied to many documents; application is best-effort, reporting and skipping a failing action rather than aborting the rest; and an opt-in `Strict` mode turns a target matching zero nodes into an error, which is what a publishing pipeline wants.

Also added `Bielu.Spec.Shared`, holding the YAML→`JsonNode` conversion previously private to `Bielu.Arazzo.NET.Readers`. Both spec libraries now share one copy of its plain-scalar type inference instead of duplicating it. `Bielu.Arazzo.NET.Readers` is unchanged in behaviour.

New `src/examples/OverlayDemo` applies an overlay to an AsyncAPI document end to end — removing an internal channel, filtering internal servers with an RFC 9535 filter function, merging into `info`, and replacing a primitive in place.
