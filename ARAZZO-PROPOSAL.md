# Proposal: Arazzo support in the Bielu API-description ecosystem

Status: **decisions taken — ready to schedule** · Date: 2026-07-26 · Companion to [ROADMAP.md](ROADMAP.md)

---

## 1. Why now — the finding that changes the calculus

[Arazzo 1.1.0](https://spec.openapis.org/arazzo/latest.html) was published **17 May 2026**, and this
minor release **adds AsyncAPI support for the first time**. Verified directly against the spec text:

| 1.1.0 addition | Spec location | Why it matters here |
|---|---|---|
| `sourceDescriptions[].type` now allows **`"asyncapi"`** (was `openapi` \| `arazzo`) | §5.8.3 Source Description Object | An Arazzo workflow can now reference *our* generated documents as a first-class source |
| `$message.header.*` and `$message.payload#/…` runtime expressions | §5.9 Runtime Expressions | Step outputs/criteria can read event payloads, not just HTTP responses |
| Step-level `dependsOn`, explicitly motivated by async coordination | §5.8.5.2.2 "Use Case: Async Coordination" | Join points for in-flight async work — e.g. "wait for the order-created event before querying status" |
| `$self`, Selector Object, Expression Type Object (`jsonpath` rfc9535 default, `xpath` 3.1 default, `jsonpointer` rfc6901) | §5.8.12, §5.6.2 | Versioned, portable reference + condition semantics |
| Fragment resolution rules spelled out for OpenAPI **and AsyncAPI** source descriptions | §5.6.2 | `operationPath: '{$sourceDescriptions.events.url}#/operations/sendLightMeasurement'` is legitimate |

Arazzo is now **the only standard way to describe a workflow that spans HTTP requests and
event/message channels.** That is precisely the seam this repository already sits on.

### The market gap

- **NuGet has zero Arazzo packages.** A search for `arazzo` on the NuGet search index returns
  `totalHits = 0`. No model, no reader, no runner, nothing.
- Existing Arazzo tooling (Redocly, Speakeasy, Bruno, Specmatic, APIDog) is **JavaScript/Java and
  OpenAPI-centric**. None of it is AsyncAPI-aware, because AsyncAPI support is eight weeks old.
- This repo is the only .NET codebase that already owns **both** halves of the problem: it generates
  AsyncAPI documents, it already has protocol drivers (SignalR, gRPC, SSE, WebRTC), and roadmap
  PRs 10–12 add broker transports (Kafka, MQTT, AMQP) that an async workflow runner needs anyway.

So the opportunity is not "add another serializer". It is: **be the reference .NET implementation of
Arazzo, and the first implementation anywhere that can actually execute a mixed HTTP + event
workflow.**

---

## 2. Naming — decided

**Packages keep their spec-named IDs. Only the repository (and docs framing) gets a broader name.**

The spec library ships under its own spec-named IDs — `Bielu.Arazzo.NET` and `Bielu.Arazzo.NET.Readers`
(see §3.A) — while a later ASP.NET Core integration package would sit beside `Bielu.AspNetCore.AsyncApi.*`
as `Bielu.AspNetCore.Arazzo.*`.

Rationale, with the costs that were checked:

- **`ApiSchemas` would have been the wrong word.** In this domain "schema" means *JSON Schema* —
  payload shapes. The codebase already uses it that way (`Services/Schemas/AsyncApiJsonSchemaService.cs`,
  `AsyncApiJsonSchemaExtensions`), and there is an unrelated published package `bielu.SchemaGenerator.Core`.
  The OAI's own term of art is **description** ("Arazzo Description", "OpenAPI Description") — so if the
  repo wants an umbrella noun, it is *descriptions*, not *schemas*.
- **Renaming published package IDs would cost real discoverability.** `asyncapi` returns 74 NuGet
  packages and the incumbent (Saunter, 3.5M downloads) competes on exactly that term; dropping
  `AsyncApi` from the ID right before the stable launch would make the suite invisible for its primary
  search term. Conversely `Bielu.AspNetCore.Arazzo` gets a search term with **zero competition**.
- **Per-spec package IDs are the ecosystem convention** — `Microsoft.OpenApi`, `ByteBard.AsyncAPI.NET`.
  One package, one spec it implements. A reader looking at a dependency list learns something.
- **The repo rename is cheap and GitHub redirects** old clone/URL paths. Touch points: `RepositoryUrl` /
  `PackageProjectUrl` in `src/Directory.Build.props`, the CI badge URL in `README.md`, doc links,
  `repository` fields in the npm workspaces, and the docs domain.

**Repo name: `Bielu.AspNetCore.ApiDescriptions`** — accurate umbrella, matches OAI vocabulary, leaves
room for Overlay or OpenAPI-side packages later.

**Docs site: a new neutral domain** (e.g. `apis.bielu.pl`), with `asyncapi.bielu.pl` redirecting to it.
Keeping the old domain would mean a host name that says AsyncAPI while documenting Arazzo as a headline
feature. This makes `PackageProjectUrl` in `src/Directory.Build.props` a pre-1.0 edit — it is currently
`https://asyncapi.bielu.pl/` and is inherited by *every* package, so changing it after the stable tag
would leave 1.0.0 packages pointing at the old host forever. Do it before the tag (see §4).

Publishing state that makes this the right moment: only `1.0.0-beta.*` prereleases are on NuGet
(31 betas of the core, 10 of `.Scalar`); **no stable 1.0.0 exists yet**, and the current branch is
`feature/preparing-stable-release`. Optional later convenience: a meta-package
`Bielu.AspNetCore.ApiDescriptions` that simply references both families.

---

## 3. Proposed packages

Five new projects, phased (no Scalar plugin — see §3.E). Each follows the conventions already in force:
`net10.0` (netstandard2.0 for
generators), central versions in `Directory.Packages.props`, shared metadata from `src/Directory.Build.props`,
single shared version via `version.props`, a `PACKAGE.md`, PublicAPI analyzer baselines, `.slnx` entry,
and a `.changeset/*.md` per PR.

### A. `Bielu.Arazzo.NET` — the spec library (build this first)

**This is the foundation and it should be built first, as a deliberate structural clone of
[ByteBard/AsyncAPI.NET](https://github.com/ByteBardOrg/AsyncAPI.NET).** It is framework-free — no
ASP.NET dependency — so the CLI, the analyzers, the runtime, and third parties all consume it. Nothing
like it exists in .NET today.

Cloning ByteBard's architecture is not cosmetic. It buys a proven layering for exactly this problem
shape, and it means anyone already using `ByteBard.AsyncAPI.NET` (which this repo depends on) meets
familiar types, naming, and lifecycle when they pick up Arazzo.

**Two packages, mirroring their split** (their model package avoids the YAML dependency; the readers
package owns it):

| Ours | Theirs | Contents |
|---|---|---|
| `Bielu.Arazzo.NET` | `ByteBard.AsyncAPI.NET` | Models, writers, validation, workspace, JSON pointer, settings, version enum |
| `Bielu.Arazzo.NET.Readers` | `ByteBard.AsyncAPI.NET.Readers` | JSON/YAML/stream/text readers, parse nodes, parsing context, diagnostics |

No `.Bindings` equivalent is needed — Arazzo has no bindings concept.

**Naming detail worth copying:** ByteBard's `PackageId` carries a `.NET` suffix while the project,
assembly, and namespace do not (`ByteBard.AsyncAPI.NET` → `ByteBard.AsyncAPI`). Do the same:
`PackageId` = `Bielu.Arazzo.NET`, assembly/namespace = `Bielu.Arazzo`. Note that
`src/Directory.Build.props` pins `AssemblyName` to `$(MSBuildProjectName)`, so `PackageId` must be set
explicitly in the csproj.

**File layout to mirror** (their structure, transposed):

```text
src/Bielu.Arazzo/
  ArazzoVersion.cs          # V1_0, V1_1 — drives reader/writer capability (see R5)
  ArazzoSettings.cs
  ArazzoWorkspace.cs        # ← the important one, see below
  JsonPointer.cs
  Error.cs
  Models/                   # ~16 spec types (the spec is small)
    ArazzoDocument.cs  ArazzoInfo.cs  ArazzoSourceDescription.cs  ArazzoWorkflow.cs
    ArazzoStep.cs  ArazzoParameter.cs  ArazzoRequestBody.cs  ArazzoPayloadReplacement.cs
    ArazzoSuccessAction.cs  ArazzoFailureAction.cs  ArazzoCriterion.cs  ArazzoExpressionType.cs
    ArazzoSelector.cs  ArazzoReusableObject.cs  ArazzoComponents.cs  ArazzoReference.cs
    Interfaces/  References/
  Expressions/              # ← no ByteBard equivalent; our highest-value asset
    RuntimeExpression.cs  RuntimeExpressionParser.cs  ExpressionEvaluator.cs
  Writers/                  # ArazzoJsonWriter, ArazzoYamlWriter, WriterBase, LoopDetector, Scope
  Validation/               # rule-based validators + ArazzoError
  Services/
  Extensions/  Exceptions/

src/Bielu.Arazzo.Readers/
  ArazzoStringReader.cs  ArazzoStreamReader.cs  ArazzoTextReader.cs  ArazzoJsonDocumentReader.cs
  ArazzoReaderSettings.cs  ArazzoDiagnostics.cs  ReadResult.cs  ParsingContext.cs
  ParseNodes/  V1/          # version-scoped deserializers, as they do V2/ and V3/
  YamlConverter.cs
```

Two pieces carry the most weight:

- **`ArazzoWorkspace`** — ByteBard uses a workspace to resolve references across documents. For Arazzo
  this is not a nicety but the core of the model: every Arazzo document points at *other* documents
  (OpenAPI, AsyncAPI, other Arazzo files) via `sourceDescriptions`, and `operationId` /
  `operationPath` / `workflowId` must resolve *into* them. The workspace is where cross-document
  resolution lives — and it is the hook that §3.B plugs the app's own live documents into.
- **`Expressions/`** — the runtime-expression parser for the ABNF in §5.9 (`$url`, `$method`,
  `$statusCode`, `$request.*`, `$response.*`, `$message.*`, `$inputs.*`, `$outputs.*`, `$steps.*`,
  `$workflows.*`, `$sourceDescriptions.*`, `$components.*`, `$self`). ByteBard has no counterpart
  because AsyncAPI has no expression language. It is the single most reusable artifact in this
  proposal, and every other package depends on it.

Also worth carrying over from their repo: per-model `SerializeV1_0` / `SerializeV1_1` methods behind an
`IArazzoSerializable` interface (their `SerializeV2`/`SerializeV3` pattern), `Resource.resx` for
diagnostic messages, and criterion evaluation for `simple` / `regex` / `jsonpath` / `xpath` (see R1).

Deps, all verified present on NuGet today: `YamlDotNet` 18.1.0 (readers only), `JsonPath.Net` 3.0.2
(RFC 9535), `JsonPointer.Net` 7.0.2, `JsonSchema.Net` 9.4.0 (validates `workflow.inputs`).
`IsAotCompatible` throughout, since the CLI and AOT story already matter here.

Keeping `YamlDotNet` confined to the readers package is the whole reason the two-package split survives:
source generators, analyzers, and anyone who only *builds and writes* documents get the model with no
YAML dependency in their graph. Splitting later would be a breaking change, so it is done up front.

#### Where it lives, how it is versioned, what it targets

**Decided: inside this repo, on the shared suite version, `net10.0` only.** This keeps the delivery
machinery boring — existing CI, existing changeset gate, existing test conventions, one `VersionPrefix`
in `version.props`, no adapter work, no polyfill layer. Two consequences to accept knowingly:

- The spec library's version will move when unrelated packages change. That is a coherent story to tell
  ("one suite, one version") as long as the docs say so plainly; the cost lands on outside consumers who
  see 1.4.0 and wonder what changed in the model. Mitigate with a per-package changelog section.
- `net10.0`-only bounds the standalone audience to .NET 10 adopters. Since Arazzo tooling is greenfield
  everywhere, early adopters skew current, so this is a defensible bet — and it means we do **not** need
  ByteBard's `Polyfill.cs`, and can use modern C# freely in the model layer. Revisit only if real demand
  appears from older targets; adding a TFM later is not a breaking change.

Two constraints still worth enforcing in review:

- **Override the packed README.** `Directory.Build.props` packs the root `README.md` into every package;
  the Arazzo packages must ship their own `PACKAGE.md`, not the AsyncAPI pitch.
- **No dependency on anything else in `src/`.** Not for extraction's sake any more, but because it is
  what keeps the library honestly framework-free and consumable by the CLI, analyzers, and outside users.
  If it ever does warrant its own repository, this constraint is what makes the move cheap — the version
  line would then need a deliberate discontinuity, which is a known, one-time cost.

Reaching for the same distribution channel they used: ByteBard ships a `.asyncapi-tool` file to get
listed in the AsyncAPI tooling registry. The Arazzo equivalent is the OAI implementations/tooling list —
being the first .NET entry there is most of the marketing this package needs.

### B. `Bielu.AspNetCore.Arazzo` — code-first authoring + serving

Mirrors the core AsyncAPI package's shape exactly, so the API feels native:

```csharp
builder.Services.AddArazzo(options =>
{
    options.WithInfo("Streetlights workflows", "1.0.0");

    options.AddWorkflow("measureAndAlert", wf => wf
        .WithInputs<MeasureInputs>()
        .Step("publishMeasurement", s => s
            .Operation("$sourceDescriptions.events.sendLightMeasurement")
            .Payload(new { lumens = "$inputs.lumens" })
            .SuccessCriteria("$message.payload#/status == 'accepted'")
            .Output("measurementId", "$message.payload#/id"))
        .Step("awaitAlert", s => s
            .DependsOn("publishMeasurement")
            .Operation("$sourceDescriptions.events.receiveLightingAlert")
            .SuccessCriteria("$message.payload#/measurementId == $steps.publishMeasurement.outputs.measurementId")));
});

app.MapArazzo();   // → /arazzo/{documentName}.json | .yaml
```

**Fluent-first, attributes later.** A workflow is an ordered sequence with data flow between steps;
attributes express that awkwardly (you end up encoding order and references in strings anyway). Ship the
builder first, and add `Bielu.AspNetCore.Arazzo.Attributes` only if demand appears.

**The differentiating feature — self-wiring source descriptions.** `AddArazzo` can resolve
`sourceDescriptions` against the documents the *same app* serves:

- the AsyncAPI documents from `IAsyncApiDocumentProvider` / `MapAsyncApi()` (`type: asyncapi`), and
- the OpenAPI document from `Microsoft.AspNetCore.OpenApi` (`type: openapi`).

Which then unlocks **cross-spec reference validation at startup and build time**: every
`operationId` / `operationPath` in every step is resolved against the live in-memory `AsyncApiDocument`
and `OpenApiDocument`, so a renamed channel or operation fails the build instead of failing in
production. *No other implementation can do this*, because no other implementation holds both sides in
one process. This is the strongest argument for Arazzo living in this repo rather than as a standalone
library.

### C. `Bielu.Arazzo.Cli` → `dotnet arazzo`

A separate tool rather than new verbs on `dotnet asyncapi`: the runner persona is CI-shaped and distinct,
and a separate package ID is independently discoverable for the uncontested `arazzo` search term. It
reuses `Bielu.Arazzo.NET` internals; the existing hand-rolled command pattern
(`Cli/Program.cs` dispatch + `Commands/*Context.cs` + `Commands/*Worker.cs`) carries over directly.

- `arazzo validate --file <path> [--strict] [--format text|json]` — spec validation **plus** reference
  resolution against fetched source descriptions.
- `arazzo lint` — authoring best practices (§5.8.5.2.3 `dependsOn` guidance, naming conventions).
- `arazzo diff --base <old> --head <new> [--fail-on-breaking]` — reuses the classification approach and
  test patterns already built for `AsyncApiDocumentComparer`.
- `arazzo run <file> --workflow <id> --input k=v [--dry-run]` — **the executor** (see D).
- `arazzo test <file> [--format junit]` — workflows as CI contract tests. This is the killer app:
  executable, spec-described integration tests that span HTTP calls *and* events.

### D. `Bielu.Arazzo.Runtime` (+ transport drivers) — the executor

The step-execution engine: expression evaluation, criterion checks, `onSuccess`/`onFailure`
(`end`/`retry`/`goto`) with `retryAfter`/`retryLimit`, `dependsOn` join points, output propagation,
and workflow-to-workflow calls.

Transports behind one `IArazzoStepTransport` seam:

- **HTTP** (`HttpClient`) — built in, covers `type: openapi` steps.
- **AsyncAPI steps** — delegate to drivers this repo already has or is already building. Note the
  synergy: the `IBrokerBridge` abstraction planned in **roadmap PR 10** (`PublishAsync` +
  `TailAsync` → `IAsyncEnumerable<BrokerMessage>`) is *exactly* the publish/await primitive an
  async Arazzo step needs. Kafka/MQTT/AMQP come free once PRs 10–12 land; SignalR and gRPC reuse the
  existing protocol packages.

Scope boundaries, stated up front: no persistence, no durable resumption, no fan-out parallelism beyond
what `dependsOn` implies, no scheduling. It is a test/automation runner, **not** a workflow engine — see
risk R3.

### E. Scalar — upstream, **not** a plugin

**No `@bielu/scalar-arazzo` console.** Arazzo support is being contributed to Scalar upstream, so
building a plugin here would duplicate that work and then have to be retired. This mirrors the approach
already taken with the `pluginUrls` contribution: fix it in Scalar, not around Scalar.

What remains on our side is **interop**, which is small but must not be forgotten:

- Serve Arazzo documents at a predictable, discoverable URL (`/arazzo/{documentName}.json|yaml`) with
  correct content types and the same CORS/auth posture as the AsyncAPI endpoints.
- A thin `ScalarOptions` extension (e.g. `WithArazzoDocument(…)`) that registers our document URLs into
  Scalar's configuration once upstream lands, following the shape the upstream feature settles on.
- Make sure the `sourceDescriptions` we emit use URLs that resolve **from the browser**, not just
  in-process — the self-wiring in §3.B must emit externally reachable URLs, or upstream Scalar cannot
  follow them.

Tracking the upstream feature's config shape before finalising §3.B's URL emission avoids a rework.

### F. Analyzers, source generator, templates, docs

- New `BARAZZO0xx` rules in the existing analyzer package: unresolvable `operationId`, dangling `goto`
  target, step output referenced but never declared, malformed runtime expression, `dependsOn` cycle.
- Source-generated metadata provider for AOT, mirroring `AddAsyncApiGeneratedMetadata()`.
- `dotnet new asyncapi-arazzo` template + a docfx `articles/arazzo.md` page.

---

## 4. Phasing

Arazzo is a **1.1 / 2.0 theme, not a 1.0 blocker.** Ship stable 1.0.0 with the AsyncAPI surface as-is.
The existing extension seams (`IAsyncApiDocumentProvider`, `IAsyncApiMetadataProvider`, transformers,
`AsyncApiOptions`) are already sufficient — **nothing in the core needs to change before 1.0 tags.**

The only decision worth making *before* the tag is the repo/docs naming (§2), because that is the part
that gets expensive later.

Continuing ROADMAP.md numbering:

| PR | Scope | Size | Depends on |
|----|-------|------|-----------|
| **pre-tag** | Repo rename + docs domain migration — **must land before stable 1.0.0** | S | nothing |
| **13** | `Bielu.Arazzo.NET` + `.Readers` — models, writers, readers, workspace, expression parser | L | none (can start immediately, parallel to 10–12) |
| **14** | `Bielu.AspNetCore.Arazzo` — builder, `MapArazzo()`, self-wiring sources, cross-spec validation | L | 13 |
| **15** | `dotnet arazzo` — `validate` / `lint` / `diff` | M | 13 |
| **16** | `Bielu.Arazzo.Runtime` + HTTP transport, `arazzo run` / `test` | L | 13, 15 |
| **17** | Async transports for the runtime (SignalR/gRPC now, brokers after PR 10–12) | M | 16, 10–12 |
| **18** | Analyzers, source generator, template, docs page, Scalar interop shim | M | 14, 16, upstream Scalar |

PR 13 is fully independent — no dependency on anything else in `src/`, by design (§3.A) — so it can
proceed in parallel with the broker work without contention, and it is the natural place to start.

### The pre-tag rename, concretely

Small but genuinely time-boxed to *before* the stable tag, because package metadata is immutable once
published:

1. Rename the GitHub repository (old URLs and clone paths redirect automatically).
2. `src/Directory.Build.props` — `RepositoryUrl` → new repo, `PackageProjectUrl` → new docs domain.
   Both are inherited by every package, which is why this cannot wait.
3. `README.md` — the CI badge URL embeds the repo name; also the docs link.
4. Docs domain: DNS + Pages config for the new host, redirect from `asyncapi.bielu.pl`, and update
   `docs/` cross-links.
5. `repository` fields in the npm workspace manifests (`@bielu/scalar-*`).
6. `AGENTS.md` package table intro and any doc references to the old repo name.

No namespace, project, or package ID changes — that is the entire point of the §2 decision.

---

## 5. Risks

**R1 — XPath 3.1 is not available in .NET.** The spec defaults `xpath` criteria to XPath 3.1; the BCL
(`System.Xml.XPath`) implements only 1.0, the `XPath2` package stops at 2.0, and Saxon-HE is a Java port
with licensing friction. *Mitigation:* fully support `simple`, `regex`, `jsonpath` (RFC 9535 via
`JsonPath.Net`) and `jsonpointer`; support `xpath-10` via the BCL and emit a clear diagnostic for
requested versions we cannot honour, using the Expression Type Object's own version negotiation. Document
the limitation honestly rather than silently mis-evaluating — §5.8.11.4.5 already requires unevaluable
conditions to *fail*, which gives us conforming behaviour.

**R2 — AsyncAPI step semantics are under-specified.** Arazzo 1.1 permits `type: asyncapi` sources and
adds `$message.*`, but the mapping of a step onto a send-vs-receive operation, how a reply is correlated
back to a request, and what `$statusCode` means for a broker message are not spelled out. *Mitigation:*
spike this first in PR 13/14, pick explicit semantics, document them as our interpretation — and raise
the ambiguity upstream as an OAI issue. Being early here is leverage: as the only AsyncAPI-aware
implementation, our interpretation has a real chance of shaping the spec, and PR-ing clarifications is a
credible route onto the OAI tooling list.

**R3 — Executor scope creep.** An Arazzo runner is one refactor away from being a workflow engine. Hold
the boundaries in §3.D and revisit only on demand.

**R4 — Security.** `arazzo run` makes real API calls with real credentials. Keep it CLI/test-only; do
**not** expose an execute endpoint from the ASP.NET package by default. If the Scalar console gains
step-through execution it must follow the same stance the broker bridge takes: nothing mapped unless
explicit, `AllowAnonymous` off outside Development, a loud startup log, and the convention builder
returned so `.RequireAuthorization(…)` is available.

**R5 — Spec churn.** 1.1.0 is eight weeks old and the tooling ecosystem is young. *Mitigation:* the spec's
own §5.1 says tooling `SHOULD NOT` distinguish patch versions; model the `arazzo` version field as a
writer/reader capability the way ByteBard handles multiple AsyncAPI versions, so a 1.2 is additive.

**R6 — Upstream Scalar timing.** §3.E depends on a feature landing in Scalar that is in progress. If the
upstream config shape is not settled by the time PR 14 emits `sourceDescriptions` URLs, we may guess
wrong and rework. *Mitigation:* keep the Scalar shim out of PR 14 entirely (it is scheduled in PR 18),
and make URL emission configurable rather than hard-coded.

---

## 6. Decisions taken

All settled 2026-07-26:

1. **Package IDs keep their spec names.** New family is `Bielu.AspNetCore.Arazzo.*`; nothing existing is
   renamed. `ApiSchemas` was rejected — "schema" already means JSON Schema in this codebase, and the OAI's
   term of art is *description*.
2. **Repo renamed to `Bielu.AspNetCore.ApiDescriptions`**; docs move to a new neutral domain with
   `asyncapi.bielu.pl` redirecting. Both land **before** the stable 1.0.0 tag (§4).
3. **The spec library comes first**, as a structural clone of ByteBard/AsyncAPI.NET, framework-free, with
   no dependency on anything else in `src/`.
4. **Two packages:** `Bielu.Arazzo.NET` + `Bielu.Arazzo.NET.Readers`, keeping `YamlDotNet` out of the
   model's dependency graph. Done up front because splitting later breaks consumers.
5. **Shared suite version, `net10.0` only** — boring machinery, no changeset adapter work, no polyfills.
   Trade-offs accepted knowingly in §3.A.
6. **Separate `dotnet arazzo` tool** rather than verbs on `dotnet asyncapi`.
7. **No Scalar plugin.** Arazzo support is going into Scalar upstream; our scope is interop only.

## 7. What still needs answering — by investigation, not decision

No open choices remain. Three things need *findings* before the code they affect is written:

1. **AsyncAPI step semantics (R2) — spike in PR 13/14.** How a step maps onto an AsyncAPI send-vs-receive
   operation, how a reply correlates back to a request, and what `$statusCode` means for a broker
   message. Produce a written interpretation, then raise the ambiguity with OAI.
2. **Upstream Scalar's Arazzo config shape (R6)** — needed before PR 14 finalises how
   `sourceDescriptions` URLs are emitted. Track the upstream work; keep emission configurable meanwhile.
3. **XPath support boundary (R1)** — confirm which criterion types real-world Arazzo documents actually
   use before investing in XPath at all. If `simple` + `jsonpath` covers the corpus, XPath 1.0 via the
   BCL plus a clear diagnostic is the whole story.
