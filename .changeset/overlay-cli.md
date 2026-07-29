---
"bielu-aspnetcore-asyncapi": minor
---

Added `Bielu.Overlay.Cli` (tool command `dotnet-overlay`), a CLI for the OpenAPI Overlay support added alongside it. `apply` transforms a description with one or more overlays; `validate` checks overlay documents on their own terms, without a target description in hand, which is what makes it usable as a CI gate on the overlays themselves.

Repeated `--overlay` arguments are applied **in the order given**, each against the result of the last — the same sequencing the specification requires of actions within a single overlay — so `--overlay strip-internal.yaml --overlay add-metadata.yaml` strips first and then annotates what survived. Nothing is written when application reports errors, so a partially transformed document is never left for a later build step to pick up. `--strict` promotes a `target` that matches zero nodes from a warning to an error: the specification permits zero matches, but in a pipeline that almost always means the overlay has drifted out of sync with the description, and failing the build beats silently publishing an untransformed document.

Since the engine works on a `JsonNode` tree rather than a typed object model, `--file` accepts OpenAPI, AsyncAPI, or Arazzo descriptions in either JSON or YAML. Output format follows `--format` when given, otherwise the `--output` extension (`.yaml`/`.yml` → YAML) as `dotnet asyncapi merge` already does, and defaults to JSON when writing to standard output so the command pipes cleanly.

Supporting that round-trip, `Bielu.Spec.Shared` gained `JsonNodeToYamlConverter`, the inverse of its existing `YamlToJsonNodeConverter` — without it a YAML description could only ever be written back out as JSON. Strings that would otherwise read back as another type are quoted, which matters for values like Arazzo's `channelPath` (`{$sourceDescriptions.events.url}#/channels/...`), where an unquoted leading `{` would parse as a YAML flow mapping.

Fixed while building it: `YamlToJsonNodeConverter` did not treat an **empty plain scalar** as null, so a YAML field written `description:` with no value was read as an empty string rather than null. Quoted `""` was, and remains, an empty string. This also affects `Bielu.Arazzo.NET.Readers`, which shares the converter.
