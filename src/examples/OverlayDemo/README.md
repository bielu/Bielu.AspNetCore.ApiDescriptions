# OverlayDemo

Applies an [OpenAPI Overlay](https://spec.openapis.org/overlay/latest.html) to an **AsyncAPI** document
using `Bielu.Overlay.NET`.

```bash
dotnet run
```

## What it shows

The Overlay Specification is written against OpenAPI, and the other .NET implementations are built on
the `Microsoft.OpenApi` object model — so they can only transform an `OpenApiDocument`. Nothing in the
overlay *mechanism* is OpenAPI-specific, though, so `Bielu.Overlay.NET` operates on
`System.Text.Json.Nodes.JsonNode` and an AsyncAPI description is just as valid a target. That is the
point of this sample.

[`public.overlay.yaml`](public.overlay.yaml) turns the internal [`asyncapi.json`](asyncapi.json) into the
version you would share with partners, exercising every action kind along the way:

| Action | What it demonstrates |
|--------|----------------------|
| remove `$.channels.internalDebug` | deleting a property by key |
| remove `$.operations.receiveInternalDebug` | keeping the document self-consistent after a removal |
| remove `$.servers[?search(@.host, 'internal')]` | an RFC 9535 **filter function**, so new internal servers are caught automatically instead of being hard-coded |
| update `$.info` | **object merge** — `title` and `version` survive, new properties are added |
| update `$.info.title` | **primitive replacement in place** (Overlay 1.1.0 only) |

It also shows that `OverlayApplier.Apply` **does not mutate the input**: after applying, the source
document still has its internal channel and original title.

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
