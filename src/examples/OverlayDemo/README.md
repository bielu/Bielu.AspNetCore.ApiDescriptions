# OverlayDemo

Applies an [OpenAPI Overlay](https://spec.openapis.org/overlay/latest.html) to an **AsyncAPI** document
and then to an **Arazzo** one, using the same `Bielu.Overlay.NET` engine for both.

```bash
dotnet run
```

## What it shows

The Overlay Specification is written against OpenAPI, and the other .NET implementations are built on
the `Microsoft.OpenApi` object model — so they can only transform an `OpenApiDocument`. Nothing in the
overlay *mechanism* is OpenAPI-specific, though, so `Bielu.Overlay.NET` operates on
`System.Text.Json.Nodes.JsonNode`, and any JSON/YAML API description is a valid target. Running one
engine over two differently-shaped specifications is the point of this sample.

### AsyncAPI — map-keyed collections

[`public.overlay.yaml`](public.overlay.yaml) turns the internal [`asyncapi.json`](asyncapi.json) into the
version you would share with partners:

| Action | What it demonstrates |
|--------|----------------------|
| remove `$.channels.internalDebug` | deleting a property by key |
| remove `$.operations.receiveInternalDebug` | keeping the document self-consistent after a removal |
| remove `$.servers[?search(@.host, 'internal')]` | an RFC 9535 **filter function**, so new internal servers are caught automatically instead of being hard-coded |
| update `$.info` | **object merge** — `title` and `version` survive, new properties are added |
| update `$.info.title` | **primitive replacement in place** (Overlay 1.1.0 only) |

### Arazzo — array-keyed collections

[`arazzo-public.overlay.yaml`](arazzo-public.overlay.yaml) performs the same shape of transformation on
[`arazzo.json`](arazzo.json), but every target has to be a **filter expression**. Arazzo keys
`workflows`, `steps`, and `sourceDescriptions` as arrays of objects carrying an id field, where OpenAPI
and AsyncAPI use maps — so there is no `$.workflows.internalDiagnostics` to target:

| Action | What it demonstrates |
|--------|----------------------|
| remove `$.workflows[?@.workflowId == 'internalDiagnostics']` | deleting an **array element** selected by its id |
| remove `$.workflows[*].steps[?@.stepId == 'dumpDebugState']` | a filter **inside** a filter, across every workflow |
| update `$.workflows[?@.workflowId == 'measureAndAlert']` | merging into a filtered object — `summary` and `steps` survive |
| update `$.sourceDescriptions` | appending to an array |
| update `$.info.title` | primitive replacement in place |

Because these removals delete array elements, several matches in one array shift each other's indexes.
The engine resolves each index against the live array at removal time, so the result is correct however
the matches are ordered.

Both runs also show that `OverlayApplier.Apply` **does not mutate the input**: afterwards each source
document still has its internal channel/workflow and its original title.

## Strict mode

The sample runs with `Strict = true`, so a `target` matching zero nodes is an error rather than a
warning. The specification permits zero matches — but in a publishing pipeline an unmatched target
almost always means the overlay has drifted out of sync with the document it transforms, and you want
that to fail the build rather than silently ship an untransformed document.

## A note on escaping

JSONPath string literals allow only the escapes `\b \f \n \r \t \/ \\ \' \"` and `\uXXXX`. A regex such
as `.*\.internal` is therefore **not** a valid literal as written — it has to be `.*\\.internal`. The
overlay here sidesteps that by matching a plain substring with `search` instead, and the library reports
the mistake as an invalid-path diagnostic if you get it wrong.
