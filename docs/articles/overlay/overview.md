# Overlay overview

`Bielu.Overlay.NET` and `Bielu.Overlay.NET.Readers` provide a framework-free object model, reader,
validator, and apply engine for the [Overlay Specification](https://spec.openapis.org/overlay/latest.html)
— the OpenAPI Initiative's companion spec for **declarative, repeatable transformations of an API
description**.

An overlay is a small JSON/YAML document listing ordered `actions`. Each action selects nodes with a
JSONPath `target`, then merges (`update`), copies (`copy`), or deletes (`remove`) at those locations.
Instead of writing imperative C# to reshape a generated document, you write the transformation down as a
reviewable artifact that lives next to the description it changes.

> ⚠️ **Note:** Before version 1.0.0, this library is regarded as unstable and **breaking changes may be
> introduced**.

## What makes this implementation different

The Overlay Specification is written against OpenAPI, and the .NET implementations that exist are built
on the `Microsoft.OpenApi` object model — so they can only transform an `OpenApiDocument`.

Nothing in the overlay *mechanism* is OpenAPI-specific, though: select by JSONPath, then
merge/copy/remove. This engine therefore operates on `System.Text.Json.Nodes.JsonNode` and nothing else,
which makes it a strict superset — **OpenAPI, AsyncAPI, and Arazzo descriptions are all just JSON/YAML
trees**, and all three are valid targets.

Applying an overlay to an AsyncAPI document is not something other tooling can do, and it is the reason
this library exists in a repository that generates AsyncAPI.

> ⚠️ **Applying overlays to non-OpenAPI documents is not officially supported by the specification.**
>
> The OAI closed [Overlay-Specification#268](https://github.com/OAI/Overlay-Specification/issues/268) as
> `not planned` in February 2026, reasoning that although it *"might technically be possible today"*, it
> *"is not a 'core function' of this specification, which is intended for OpenAPI descriptions"*.
>
> We are trying to change that. [#367](https://github.com/OAI/Overlay-Specification/issues/367) revisits
> the question, with draft PR [#370](https://github.com/OAI/Overlay-Specification/pull/370) proposing a
> `targetFormat` declaration. It argues chiefly that the OAI has **already** made this commitment
> elsewhere: released Arazzo 1.1.0 normatively defines a Source Description's `type` as
> `"openapi" | "asyncapi" | "arazzo"`, has AsyncAPI-specific behaviour (`correlationId` is *"only
> applicable to asyncapi steps with action receive"*), and resolves references against *"an OpenAPI or
> AsyncAPI description"*. So Overlay could adopt an existing OAI value set rather than invent a registry.
>
> That discussion is open and the working group remains broadly aligned with the original decision, so
> **treat AsyncAPI and Arazzo targeting as an extension this library offers, not a conformance claim.**
> Behaviour against OpenAPI documents stays spec-exact regardless, so overlays you write for OpenAPI
> remain portable to other tooling.

## Installation

```bash
dotnet add package Bielu.Overlay.NET
dotnet add package Bielu.Overlay.NET.Readers
```

`Bielu.Overlay.NET` depends only on `JsonPath.Net` (RFC 9535 JSONPath). `YamlDotNet` is confined to
`Bielu.Overlay.NET.Readers`, so consumers that only build and apply overlays in memory never pull in a
YAML parser.

## Reading an overlay

```csharp
using Bielu.Overlay.Readers;

var result = OverlayStringReader.Read(yamlOrJsonText);

if (result.HasErrors)
{
    foreach (var diagnostic in result.Diagnostics)
    {
        Console.WriteLine(diagnostic);   // "error at /actions/0/target: ..."
    }
    return;
}

var overlay = result.Document!;
```

`OverlayStringReader` auto-detects JSON vs. YAML from the input's first non-whitespace character.
`OverlayStreamReader` and `OverlayTextReader` cover the `Stream`/`TextReader` equivalents. Reading never
throws for malformed input — problems come back as diagnostics on the returned `OverlayReadResult`.

## Applying an overlay

```csharp
using System.Text.Json.Nodes;
using Bielu.Overlay;

var document = JsonNode.Parse(File.ReadAllText("asyncapi.json"));

var result = OverlayApplier.Apply(document, overlay);

Console.WriteLine(result.Document!.ToJsonString());
```

`Apply` never mutates the document you pass in — it works on a deep copy and returns a distinct tree, so
the same overlay can safely be applied to several documents in turn.

Application is **best-effort**: an action that fails is reported in `Diagnostics` and skipped, and the
remaining actions still run. Check `result.HasErrors` to decide whether to trust the output.

### Strict mode

The specification permits a `target` that matches nothing — it is simply a no-op. That is convenient
when authoring and dangerous in CI, where it usually means the overlay has drifted out of sync with the
document it transforms. `Strict` promotes those from warnings to errors:

```csharp
var result = OverlayApplier.Apply(document, overlay, new OverlayApplyOptions { Strict = true });
```

## Validating an overlay

```csharp
using Bielu.Overlay.Validation;

foreach (var diagnostic in OverlayValidator.Validate(overlay))
{
    Console.WriteLine(diagnostic);
}
```

`OverlayValidator` checks the overlay on its own terms, with no target document in hand: a recognized
version, non-empty `info.title`/`info.version`, at least one action, `target`/`copy` expressions that
actually parse as RFC 9535 JSONPath, and `copy` used only where the declared version supports it.

## A worked example: publishing a public AsyncAPI document

Suppose your service generates this internally:

```json
{
  "asyncapi": "3.0.0",
  "info": { "title": "Streetlights", "version": "1.0.0" },
  "channels": {
    "lightMeasured": { "address": "light/measured" },
    "internalDebug": { "address": "internal/debug" }
  }
}
```

An overlay strips the internal channel and adds partner-facing metadata:

```yaml
overlay: 1.1.0
info:
  title: Public distribution of the Streetlights API
  version: 1.0.0
actions:
  - target: $.channels.internalDebug
    description: Internal diagnostics channel, not for partners
    remove: true

  - target: $.info
    update:
      description: Public event API for the Streetlights platform
      x-audience: partner
```

Applying it yields a document with `internalDebug` gone and `info` enriched — with the original
untouched on disk.

## Targeting Arazzo documents

> ⚠️ As above, Arazzo is not a specification-sanctioned overlay target — see
> [#268](https://github.com/OAI/Overlay-Specification/issues/268) (closed `not planned`) and the open
> [#367](https://github.com/OAI/Overlay-Specification/issues/367) we filed to revisit it.

Arazzo works too, but it is worth knowing that it targets differently, because of how the specification
shapes its collections:

| Specification | Collection | Shape | Targeting |
|---------------|-----------|-------|-----------|
| OpenAPI | `paths` | **map** keyed by path | `$.paths['/items']` |
| AsyncAPI | `channels` | **map** keyed by name | `$.channels.lightMeasured` |
| Arazzo | `workflows`, `steps`, `sourceDescriptions` | **array** of objects carrying an id field | `$.workflows[?@.workflowId == 'measureAndAlert']` |

There is no `$.workflows.measureAndAlert` to target — a workflow is an array element, not a map entry, so
**every Arazzo target is a filter expression**. That leans directly on RFC 9535 filters, which is one
practical reason to declare `overlay: 1.1.0`: 1.0.0 never pinned the JSONPath dialect, so filter support
there varies between tools.

```yaml
overlay: 1.1.0
info:
  title: Public distribution of the Streetlights workflows
  version: 1.0.0
actions:
  # Remove a whole workflow, selected out of the array by its id
  - target: $.workflows[?@.workflowId == 'internalDiagnostics']
    remove: true

  # Remove a step from whichever workflow contains it — a filter inside a filter
  - target: $.workflows[*].steps[?@.stepId == 'dumpDebugState']
    remove: true

  # Merge into a workflow; `summary` and `steps` survive because object targets merge
  - target: $.workflows[?@.workflowId == 'measureAndAlert']
    update:
      description: Publishes a measurement, then waits for the alert it triggers.

  # Append to an array
  - target: $.sourceDescriptions
    update:
      name: events
      url: https://example.com/asyncapi.json
      type: asyncapi
```

Because removals here delete *array elements*, several matches in the same array shift each other's
indexes. The engine resolves each match's index against the live array at removal time, so
`$.workflows[*].steps[?@.stepId == 'debug']` correctly removes every matching step across every workflow
regardless of the order matches come back in.

## Action semantics

Actions apply **in sequence, each against the result of the previous one**. That is what lets an overlay
delete a node in one action and re-create it in a later one.

| `target` selects | `update` behaviour |
|------------------|--------------------|
| an object | its properties are recursively merged into the selected object |
| an array | an array **concatenates** element-wise (1.1.0); anything else appends as one entry |
| a primitive | the value replaces it in place (1.1.0 only) |

`remove` deletes the selected nodes from the object or array containing them. `copy` takes a JSONPath
selecting **a single** node elsewhere in the same document and applies it to the targets using the same
rules as `update` — sequenced with `update` or `remove`, that expresses moves and renames.

`update`, `copy`, and `remove` are not mutually exclusive in the specification. Where more than one is
present the precedence is **`remove` > `copy` > `update`** (`update` "has no impact" when outranked); the
validator reports the redundancy as a warning rather than rejecting the document.

### Interpretation notes

Two places where the specification leaves room, and what this implementation does:

- **Nested arrays during a recursive merge are replaced, not concatenated.** The concatenate/append rule
  is defined for a `target` that *selects* an array node; extending it to arrays met partway through a
  merge would make it impossible to overwrite an array at all.
- **A `null` `update` is treated as absent.** JSON `null` has no sanctioned meaning for `update` —
  deleting is `remove`'s job, not a null-merge as in JSON Merge Patch (RFC 7386).

## Version differences

The library targets 1.1.0 and accepts 1.0.0, gating semantics on the document's own `overlay` field. An
unrecognized version warns and falls back to 1.1.0 semantics rather than refusing to apply.

| Aspect | 1.0.0 | 1.1.0 |
|--------|-------|-------|
| `target` grammar | "a JSONPath expression" — unpinned | **RFC 9535** JSONPath |
| Legal target nodes | objects and arrays only | objects, arrays, **and primitives** |
| `update` on an array | appends a single entry | **concatenates** an array; appends anything else |
| `update` on a primitive | not permitted | **replaces in place** |
| `remove` of a primitive array item | not permitted | supported |
| `copy` | — | **new** |

Using a 1.1.0-only feature in a document that declares `overlay: 1.0.0` is reported as an error rather
than silently applied.

## `extends` is never dereferenced

An overlay may carry `extends`, a URI identifying the document it was written for. This library exposes
it on the model but **never fetches it**. Resolving it over the network from inside a hosted application
would let an overlay file drive outbound requests; use it at most to verify that the document you are
about to transform is the one the overlay expected.

## Applying overlays in an ASP.NET Core app

Everything above transforms a document you already have in hand. If the document is one your own app
generates, you can skip the file-in/file-out step entirely: `Bielu.AspNetCore.AsyncApi.Overlay` and
`Bielu.AspNetCore.Arazzo.Overlay` apply overlays *inside the generation pipeline*, so the document served
by `MapAsyncApi()` / `MapArazzo()` is already transformed.

```csharp
builder.Services.AddAsyncApi("v1", options =>
{
    options.AddOverlay("overlays/public.yaml");
});
```

See [Pipeline Integration](pipeline-integration.md) for ordering, strict mode, failure behaviour, and how
this interacts with build-time document generation.

## CLI Tool: `dotnet overlay`

`Bielu.Overlay.Cli` provides `apply` and `validate` commands for overlays and the descriptions they
transform — see the [CLI Tool](cli.md) article.

```bash
dotnet tool install -g Bielu.Overlay.Cli
dotnet overlay apply --file asyncapi.json --overlay public.overlay.yaml --output public.json --strict
```

## Example project

A runnable end-to-end sample lives in
[`src/examples/OverlayDemo`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/tree/main/src/examples/OverlayDemo).
It runs the same engine twice — once over an AsyncAPI document and once over an Arazzo one — printing
before/after and diagnostics for each, so the map-keyed and array-keyed targeting styles sit side by side.

```bash
cd src/examples/OverlayDemo
dotnet run
```
