# Overlay conformance fixtures

These files are **vendored verbatim** from the OpenAPI Initiative's
[Overlay-Specification](https://github.com/OAI/Overlay-Specification) repository, at commit
[`2279eb4`](https://github.com/OAI/Overlay-Specification/tree/2279eb449e9bd2e9844960bd11690ded5d252fa5)
(2026-07-28). They are licensed Apache-2.0 by the OpenAPI Initiative, the same licence this repository
uses. Do not hand-edit them — refresh them from upstream instead, and bump the commit recorded above.

They are vendored rather than fetched at test time so the suite runs offline and a network outage or an
upstream force-push can never turn into a red build.

## `compliant-sets/` — apply semantics

Eight triples of `openapi.yaml` (input), `overlay.yaml` (the transformation) and `output.yaml` (the
expected result). Upstream describes them as "known good" sets offered "with the aim of supporting people
building tools that apply Overlays" — which is precisely what `OverlayApplier` is. Driven by
`OverlayCompliantSetsTests`.

Comparison is structural (`JsonNode.DeepEquals`), not textual: the fixtures are YAML and we re-emit from a
`JsonNode` tree, so key ordering and formatting legitimately differ while the document does not.

## `documents/v1.0` and `documents/v1.1` — document validity

`pass/` overlays must be accepted, `fail/` overlays must be rejected. Driven by
`OverlayDocumentConformanceTests`.

Upstream validates these against the JSON Schemas in its `schemas/` directory, whereas we exercise
`OverlayStringReader` plus `OverlayValidator`. Those are not the same mechanism, so a handful of `fail/`
fixtures encode constraints a schema expresses but a hand-written reader/validator pair does not — each
one is listed explicitly, with its reason, in `OverlayDocumentConformanceTests.SchemaOnlyFailures`. That
list is deliberately enumerated rather than glob-matched, so adding an upstream fixture surfaces as a test
failure to be triaged rather than being silently ignored.

Upstream also ships `tests/v1.2-dev/`. Those are **not** vendored: 1.2 is an unreleased draft, and
`OverlayVersion` deliberately recognises only 1.0 and 1.1.
