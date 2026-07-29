# Overlays in the generation pipeline

`Bielu.AspNetCore.AsyncApi.Overlay` and `Bielu.AspNetCore.Arazzo.Overlay` apply overlays **while the
document is being produced**, so `GET /asyncapi/v1.json` and `GET /arazzo/workflows.json` are already
transformed. There is no build step and no second artifact to keep in sync.

Every other overlay tool — including the CLI in this repository — is file-in/file-out post-processing.
This is the piece that isn't.

```bash
dotnet add package Bielu.AspNetCore.AsyncApi.Overlay   # for AsyncAPI documents
dotnet add package Bielu.AspNetCore.Arazzo.Overlay     # for Arazzo documents
```

## Usage

```csharp
using Bielu.AspNetCore.AsyncApi.Overlay;

builder.Services.AddAsyncApi("v1", options =>
{
    options.AddOverlay("overlays/public.yaml");
});
```

The Arazzo side is identical, on `ArazzoOptions`:

```csharp
using Bielu.AspNetCore.Arazzo.Overlay;

builder.Services.AddArazzo("workflows", options =>
{
    options.AddOverlay("overlays/public-workflows.yaml");
});
```

Overlays can also be supplied in memory, which is useful when the transformation is computed rather than
authored:

```csharp
options.AddOverlay(myOverlayDocument);   // an OverlayDocument
options.AddOverlay(OverlaySource.FromFile("overlays/public.yaml"));
```

Multiple overlays apply **in the order they are added**, each against the result of the last — the same
sequencing the specification requires of actions within a single overlay:

```csharp
options.AddOverlay("overlays/strip-internal.yaml")   // runs first
       .AddOverlay("overlays/rebrand.yaml");         // then this
```

### Strict mode

A `target` that selects zero nodes is permitted by the specification, so by default it is logged as a
warning and generation continues. In CI you usually want the opposite — an overlay that has quietly
stopped matching is an overlay that has quietly stopped working:

```csharp
options.AddOverlay("overlays/public.yaml")
       .ConfigureOverlays(apply => apply.Strict = true);
```

## Where this runs, and why

Overlays are applied at the **serialization boundary**: after the document has been written out, before
those bytes reach the response or the file. The overlay therefore sees exactly what the consumer would
have seen.

It is worth being explicit about the alternative that was rejected. `AddDocumentTransformer` hands
transformers a typed `AsyncApiDocument`, so running an overlay there would mean serialize → overlay →
deserialize. That costs a round trip and, worse, stakes correctness on the serializer round-tripping
losslessly. Overlay targets are JSONPath expressions over the wire representation; there is no faithful
typed equivalent.

Concretely, this means overlays apply to:

- the `MapAsyncApi()` / `MapArazzo()` endpoints, in both JSON and YAML form;
- **build-time document generation** (`IDocumentProvider`, the `dotnet asyncapi` / MSBuild path), so a
  checked-in document and the served one never disagree about whether the overlay ran.

For YAML routes the document is converted to a `JsonNode` tree, transformed, and re-emitted as YAML —
the overlay engine only ever works on JSON trees. Comments are not preserved, and key ordering follows the
tree rather than the original file.

Prefer `AddDocumentTransformer` whenever the change *can* be expressed against the object model: it is
typed, cheaper, and cannot produce a malformed document. Reach for an overlay when the transformation
should be a reviewable artifact rather than code, or when it needs to be shared with tooling outside .NET.

## Failure behaviour

Failures are loud by design. An overlay that silently does nothing serves a description that looks right
but is missing the transformation someone depends on, so `OverlayApplicationException` is thrown when:

- the overlay file cannot be read, or is not a valid overlay document;
- the document cannot be parsed in the format it claims;
- applying an overlay reports an error (including a zero-match `target` under `Strict`).

From the document endpoint that surfaces as a `500` with an RFC 7807 problem response — never a `200`
carrying a half-transformed body, because serialization is fully buffered before any header is committed.
From build-time generation it fails the build.

Non-fatal diagnostics are logged as warnings against the `Bielu.AspNetCore.AsyncApi.Overlay` /
`Bielu.AspNetCore.Arazzo.Overlay` categories.

## Loading and caching

Overlay files are read **once, on first use**, not when services are registered — so a missing file does
not break startup, and the file is not re-read per request. Changing an overlay on disk requires a restart.

## Extending the seam

Both packages are thin adapters over a general hook, which is available directly if you need to rewrite a
serialized document some other way:

```csharp
options.AddSerializedDocumentTransformer(async (document, context, cancellationToken) =>
{
    // context.DocumentName, context.Format (Json/Yaml), context.ApplicationServices
    return document.Replace("http://", "https://");
});
```

`IAsyncApiSerializedDocumentTransformer` lives in the core package and
`IArazzoSerializedDocumentTransformer` in `Bielu.AspNetCore.Arazzo`; neither requires the overlay packages.

## Package layout

| Package | Adds `AddOverlay` to | Depends on |
|---|---|---|
| `Bielu.AspNetCore.AsyncApi.Overlay` | `AsyncApiOptions` | `Bielu.AspNetCore.AsyncApi`, `Bielu.Overlay.NET(.Readers)` |
| `Bielu.AspNetCore.Arazzo.Overlay` | `ArazzoOptions` | `Bielu.AspNetCore.Arazzo`, `Bielu.AspNetCore.AsyncApi.Overlay` |

They are separate packages so the core AsyncAPI package's dependency graph is unchanged for users who
don't want overlays, and so an AsyncAPI-only consumer never pulls in Arazzo.
