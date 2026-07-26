# Roadmap

Working agreement: **one branch/PR per feature off `main`**, each PR carries its own `.changeset/*.md`
entry (the `changeset-check` CI gate requires one). Update the checkboxes here as PRs land so work can
be picked up at any point.

Two tracked initiatives: the original **9-feature / 12-PR roadmap** below (all landed except the broker
proxy, PRs 10–12), and **Arazzo workflow support (PRs 13–18)** further down — see
[ARAZZO-PROPOSAL.md](ARAZZO-PROPOSAL.md) for that initiative's full rationale.

Decisions already made:
- Docs site: **docfx** (auto API reference from XML docs — synergy with PR 1).
- Broker proxy: **Kafka + MQTT + AMQP**, phased with Kafka first (PRs 10–12).
- Analyzers: **bundled inside `Bielu.AspNetCore.AsyncApi.Attributes`** (`analyzers/dotnet/cs` asset).

Cross-cutting facts:
- All projects `net10.0`; central package versions in root `Directory.Packages.props`; shared NuGet
  metadata in `src/Directory.Build.props`; single shared version via `version.props` + synthetic
  changeset package `build/changeset/nuget-suite`.
- New **NuGet** packages need no CI change (release workflow packs the solution and pushes
  `dist/*.nupkg`). New **npm** packages need a root `package.json` workspaces entry + a CI job trio in
  `.github/workflows/buildAndPublishPackage.yml` (mirror `ciGrpcNpm`/`releaseStableGrpcNpm`/`releaseBetaGrpcNpm`).
- New projects go into `src/Bielu.AspNetCore.AsyncApi.slnx` (`/tools/`; tests under `/tests/` as `../test/...`).
- Every PR also updates `README.md` and the `AGENTS.md` package table.

---

## PR 1 — XML doc comments → descriptions

- [x] Completed

`/// <summary>` on channels/operations/messages/payload types becomes descriptions (Swashbuckle
`IncludeXmlComments` equivalent). Runtime XML reading; AOT handled separately in PR 9.

- New `src/Bielu.AspNetCore.AsyncApi/Services/XmlDocs/`: `XmlDocumentationProvider` (parses compiler
  XML into `memberId → (summary, remarks, params)`), `XmlDocumentationIdBuilder` (`T:`/`M:`/`P:` id
  generation incl. generics).
- `AsyncApiOptions` additions: `IncludeXmlComments(string filePath)` and
  `IncludeXmlComments(Assembly)` (resolves `{AssemblyName}.xml` next to the assembly; logged warning
  if missing).
- Wire as **fallback** (attributes win) at the description set-points in
  `Services/AsyncApiDocumentService.cs` (channel ~183/192, channel param ~250, message ~344,
  operation ~442) and schema/property descriptions in `Services/Schemas/AsyncApiJsonSchemaService.cs`
  (~109/115, where `DescriptionAttribute` is read today).
- Enable `<GenerateDocumentationFile>` in example projects; use in `StreetlightsAPI`.
- Tests (`test/Bielu.AspNetCore.AsyncApi.Tests`, xunit + Shouldly): id-builder + provider units over a
  fixture XML; integration test asserting a class summary lands in the document.

## PR 2 — Message examples

- [x] Completed

Embed `examples` into generated messages; Scalar and the consoles can render/prefill them.

- Attributes package: `MessageExampleAttribute` (`Method`, `AllowMultiple`) with `Name`, `Summary`,
  `Json` (raw literal) **or** `ProviderType` implementing new `IAsyncApiMessageExampleProvider`
  (also lives in the Attributes package to keep it dependency-free).
- Fluent alternative: `AsyncApiOptions.AddMessageExample<TPayload>(string name, TPayload payload)`.
- Attach in `Services/AsyncApiDocumentService.cs` at the two `AsyncApiMessage` construction sites
  (~339, ~421) → ByteBard `AsyncApiMessage.Examples` (confirm exact `AsyncApiMessageExample` payload
  shape at impl time; serialize via `AsyncApiSerializationHelper` conventions).
- Optional flag `SetSchemaExampleFromMessageExample` (default off) to surface a single example as the
  schema `example`.
- Tests: one per source (Json literal / provider / fluent) asserting serialized output; round-trip
  through `AsyncApiStringReader` to prove spec validity.
- Follow-up (out of scope): consoles preferring real examples over `exampleFromSchema` in
  `@bielu/scalar-core`.

## PR 3 — CLI `validate` command

- [x] Completed

`dotnet asyncapi validate --file <path> [--strict] [--format text|json]`.

- CLI uses hand-rolled parsing (`src/Bielu.AspNetCore.AsyncApi.Cli/Program.cs` dispatches on
  `args[0]`). Add `Commands/ValidateCommandContext.cs` (args: repeatable `--file`/glob, `--strict`,
  `--format`) + `Commands/ValidateCommandWorker.cs`, plus `Program.cs` branch and `PrintUsage` entry.
- Read via ByteBard `AsyncApiStringReader` (same as `Merger/Merge/AsyncApiDocumentMerger.cs:129`);
  report `AsyncApiDiagnostic` errors/warnings with locations.
- Exit codes: 0 clean, 1 errors, 1 on warnings only with `--strict`. `--format json` for CI.
- Tests (`test/Bielu.AspNetCore.AsyncApi.Cli.Tests`): `ValidateCommandContextTests` +
  `ValidateCommandWorkerTests` with valid/invalid fixtures (mirror `MergeCommand*Tests`).

## PR 4 — CLI `diff` command

- [x] Completed

`dotnet asyncapi diff --base old.json --head new.json [--fail-on-breaking] [--format text|json|markdown]`.

- `Commands/DiffCommandContext.cs` + `Commands/DiffCommandWorker.cs`; documents loaded with
  `AsyncApiStringReader`. No comparison logic exists anywhere (Merger is additive union) — new
  internal `AsyncApiDocumentComparer` (CLI-internal by default) walking
  servers/channels/operations/messages/payload schemas.
- Classification — **breaking**: removed channel/operation/message, changed action/direction, payload
  schema narrowing (removed property, type change, new required property), removed server/security
  scheme. **Non-breaking**: additions, description/metadata changes.
- `--fail-on-breaking` → exit 1. Output grouped by severity.
- Tests: comparer unit per change category + worker tests over fixture pairs.

## PR 5 — Roslyn analyzers (bundled in Attributes package)

- [x] Completed

Compile-time diagnostics for attribute misuse that fails silently at runtime today.

- New `src/Bielu.AspNetCore.AsyncApi.Analyzers` — **netstandard2.0**, `Microsoft.CodeAnalysis.CSharp`
  (pin in `Directory.Packages.props`, `PrivateAssets=all`), `IsPackable=false`. Verify
  `src/Directory.Build.props` net10.0 conventions don't break a netstandard2.0 project; override
  locally if needed.
- Bundle into `Bielu.AspNetCore.AsyncApi.Attributes.csproj`: pack analyzer DLL to
  `analyzers/dotnet/cs` (`ProjectReference` with `ReferenceOutputAssembly=false` + pack item).
- Rules (match attributes by metadata name from `Bielu.AspNetCore.AsyncApi.Attributes.Attributes`):
  - **BASYNC001** (warn): `[Channel]`/operation/`[Message]` attribute whose containing type lacks
    `[AsyncApi]` — silently ignored by the scanner today.
  - **BASYNC002** (warn): operation attribute on a method with no `[Channel]` on method or type.
  - **BASYNC003** (warn): duplicate `Name` across `AllowMultiple` `[Message]`/`[ChannelParameter]`.
  - **BASYNC004** (info): `[AsyncApi("name")]` literal never appearing in any `AddAsyncApi("name", …)`
    literal in the compilation (best-effort).
  - **BASYNC005** (warn, only if cheap/quiet): `ChannelParameterAttribute.Type`/`MessageAttribute.PayloadType`
    unusable for schema gen.
  - **BASYNC006** (error): `MessageExampleAttribute` with `Json` literal that is not valid JSON.
  - **BASYNC007** (warn): `IAsyncApiMessageExampleProvider` implementation lacking a public parameterless constructor.
  - **BASYNC008** (warn): `MessageId` or `Name` containing characters discouraged by AsyncAPI spec (e.g. spaces in IDs).
  - **BASYNC009** (info): Missing `Summary` or `Description` on public `[AsyncApi]` components.
- Tests: new `test/Bielu.AspNetCore.AsyncApi.Analyzers.Tests` using
  `Microsoft.CodeAnalysis.CSharp.Analyzer.Testing`.

## PR 6 — `dotnet new` template pack

- [x] Completed

`dotnet new install Bielu.AspNetCore.AsyncApi.Templates` → `asyncapi-webapi | asyncapi-signalr | asyncapi-grpc | asyncapi-console | asyncapi-sln`.

- New `src/Bielu.AspNetCore.AsyncApi.Templates`: `PackageType=Template`, `IncludeContentInPack=true`,
  `IncludeBuildOutput=false`. Inherits shared metadata/version; ships via existing dist glob — no CI
  change.
- Templates (each with `.template.config/template.json`, `sourceName`, framework param):
  - `asyncapi-webapi` — minimal API + `[AsyncApi]` bus + `MapAsyncApi()` + Scalar (distilled StreetlightsAPI).
  - `asyncapi-signalr` — hub + bindings + SignalR console (distilled `src/examples/SignalRChat`).
  - `asyncapi-grpc` — greeter + gRPC-Web + gRPC console (distilled `src/examples/GrpcGreeter`).
  - `asyncapi-console` — worker service + `[AsyncApi]` + XML docs (Console App).
  - `asyncapi-sln` — multi-project solution with Contracts, Api, and Worker (Whole Solution).
- Documentation: dedicated [Templates](docs/articles/templates.md) page.
- Template content references published package versions (1.0.0 for first release).
- Verification: smoke script (`scripts/test-templates.ps1`) or manual instantiation.

## PR 7 — docfx documentation site

- [x] Completed

Real docs site on GitHub Pages; README shrinks to pitch + quickstart.

- Greenfield: no docs source exists. Current `.github/workflows/deployDocs.yml` is dead (triggers on
  `master`, unzips a stale `webHelpADOC2-all.zip`) — **replace it**.
- New `docs/`: `docfx.json` (modern template), `index.md`, `articles/` split from README
  (getting-started, attributes, configuration, build-time-generation, cli incl. validate/diff,
  protocols signalr/sse/webrtc/grpc, scalar-consoles, merger/gateway, multiple-documents,
  migration-from-saunter), `api/` docfx metadata from packable src projects (needs
  `GenerateDocumentationFile=true` on libraries — also feeds PR 1).
- New workflow: on push to `main` (docs/src paths) — docfx build, `configure-pages@v5`,
  `upload-pages-artifact@v3`, `deploy-pages@v4`.
- README: keep badges, pitch, install, one quickstart, links into the site.

## PR 8 — Asp.Versioning integration

- [x] Completed

Document-per-API-version, matching the OpenAPI-side convention.

- New `src/Bielu.AspNetCore.AsyncApi.Versioning` referencing `Asp.Versioning.Mvc.ApiExplorer` + core.
- Surface: `services.AddAsyncApiForApiVersions(Action<AsyncApiOptions, ApiVersionDescription>?)` —
  enumerates `IApiVersionDescriptionProvider.ApiVersionDescriptions`, calls existing
  `AddAsyncApi(groupName, …)` per version (named docs already map to `/asyncapi/{documentName}.json`
  via keyed services — `Extensions/AsyncApiServiceCollectionExtensions.cs`), sets
  `WithInfo(title, version)`, marks deprecated versions.
- Filtering: endpoint-path via existing `AsyncApiOptions.ShouldInclude` (`Func<ApiDescription,bool>`)
  matched on `GroupName`; attribute-scanned classes use the existing `[AsyncApi("v1")]` doc-name
  mechanism (convention: doc name == version group name).
- Implementation: uses `KeyedService.AnyKey` to handle versioned documents dynamically; registers
  `IAsyncApiDocumentNamesProvider` for discovery of versioned document names.
- Example: tested via `Bielu.AspNetCore.AsyncApi.Versioning.Tests`.
- Tests: new `test/Bielu.AspNetCore.AsyncApi.Versioning.Tests` — WebApplicationFactory-like app with two
  versions → two documents with correct info/channels.

## PR 9 — Native AOT support via source generator

- [x] Completed

Make generation work under `PublishAot`/trimming. Biggest core refactor; do after PRs 1–2 so the
metadata model is stable. **Guaranteed: this refactor will not break current reflection-based behavior;
the reflection provider remains the default fallback.**

1. **Seam refactor (core):** extract the discovery loop in `Services/AsyncApiDocumentService.cs`
   (AppDomain scan ~498–511, `GetCustomAttribute`/`GetMethods` walk ~139–442) behind
   `IAsyncApiMetadataProvider` returning a POCO model (`AsyncApiTypeMetadata` →
   channels/operations/messages/parameters). Default impl = current reflection scan. Remove the
   reflective property write at ~517 (`GetType().GetProperty`).
2. **Generator:** new `src/Bielu.AspNetCore.AsyncApi.SourceGenerators` (netstandard2.0, incremental
   `IIncrementalGenerator`, packed into the **core** package's `analyzers/dotnet/cs`). Finds
   `[AsyncApi]` types, emits `GeneratedAsyncApiMetadataProvider` + explicit
   `services.AddAsyncApiGeneratedMetadata()` extension; when registered, reflection scan is skipped.
3. **Trim/AOT hygiene:** `IsAotCompatible` on core + Attributes; fix analyzer warnings
   (`Services/AsyncApiGenerator.cs` ~349, `Services/Schemas/AsyncApiJsonSchemaService.cs`
   ~53/109/115/690). Schema gen uses STJ `JsonTypeInfo` — document that AOT apps supply a
   source-generated `JsonSerializerContext` via the `AsyncApiJsonSchemaJsonOptions` path.
- Out of scope: AOT-cleaning the removed UI package / Scalar packages beyond what falls out.
- Verification: new `src/examples/AotStreetlights` with `<PublishAot>true</PublishAot>`; CI step
  publishing it and snapshot-comparing its document against the reflection-based output.

## PRs 10–12 — Broker console proxy (Kafka → MQTT → AMQP)

Invocable Kafka/MQTT/AMQP channels in Scalar via an opt-in server-side bridge (browsers can't speak
these protocols). Mirrors the gRPC console structure — its `/descriptors` extra endpoint
(`src/Bielu.AspNetCore.AsyncApi.Scalar.Grpc/ScalarGrpcEndpointRouteBuilderExtensions.cs`) is the
precedent for mounting server-side data endpoints next to the bundle.

### PR 10 — core bridge + Kafka + console (MVP)

- [ ] Not started

**.NET:**
- `src/Bielu.AspNetCore.AsyncApi.Scalar.Broker` (mirror gRPC package shape): `ScalarBrokerDefaults`
  (`AssetsPath = "/bielu/scalar/broker"`), `ScalarBrokerOptions : ScalarPluginDocumentOptions<>`,
  `WithBrokerClient()` → shared `WithAsyncApiPluginScript` (global `__BIELU_SCALAR_BROKER__`),
  `MapScalarBrokerAssets()` → shared `MapScalarPluginBundle` + data endpoints:
  - `GET {path}/connections` — configured connections (name, protocol, redacted endpoint).
  - `POST {path}/publish` — `{connection, channel/topic, key?, headers?, payload}` → one message; returns ack metadata.
  - `GET {path}/tail?connection=…&channel=…` — SSE stream of consumed messages (ephemeral consumer).
- Abstraction `IBrokerBridge` (`PublishAsync`, `TailAsync` → `IAsyncEnumerable<BrokerMessage>`),
  registry keyed by connection name; `services.AddScalarBrokerBridge(o => o.AddConnection(…))`.
- **Security (must-have):** nothing mapped unless explicit; `MapScalarBrokerAssets` returns the
  convention builder for `.RequireAuthorization(…)`; `AllowAnonymous` defaults false outside
  Development with a loud startup log. Document: this bridge grants publish access to your broker.
- `src/Bielu.AspNetCore.AsyncApi.Scalar.Broker.Kafka` — `KafkaBrokerBridge` on `Confluent.Kafka`;
  `AddKafkaConnection(name, bootstrapServers, configure?)`; tail via ephemeral consumer (random group
  id, `AutoOffsetReset.Latest`).

**npm** `@bielu/scalar-broker` (`src/Bielu.AspNetCore.AsyncApi.Scalar.Broker/assets`, built on
`@bielu/scalar-core` like `@bielu/scalar-grpc`):
- `index.ts`/`plugin.ts` (element `bielu-broker-console`, plugin `bielu-broker`, wrappedFlag
  `__bieluBrokerWrapped`), `discovery.ts` (configKey `broker`, global `__BIELU_SCALAR_BROKER__`),
  `broker-bindings.ts` (parse `kafka` bindings; prefill via `exampleFromSchema` or real examples from
  PR 2), `components/BrokerConsole.vue` (channel list, publish form, tail log).
- Transport = the proxy endpoints: `fetch` POST for publish; **fetch-streaming (ReadableStream) for
  tail, not `EventSource`** (EventSource can't send auth headers; Scalar auth passthrough via core
  `resolveSelectedSchemes` → headers).
- Vite config copied from gRPC assets; standalone bundle via existing `build-standalone.mjs` pattern.

**Infra:** root `package.json` workspaces entry; new CI npm job trio in
`buildAndPublishPackage.yml`; `ScalarPluginBundle.targets` import; `.slnx` entries.

**Example + tests:** wire into the Aspire mini-shop (already runs Kafka) — order-events tail +
publish. `test/Bielu.AspNetCore.AsyncApi.Scalar.Broker.Tests` copying `ScalarGrpcEndpointTests`
patterns (bundle 200 + element tag; connections/publish/tail against a fake `IBrokerBridge`;
missing-bundle 404 message). Kafka bridge e2e via the Aspire example (Testcontainers only if
acceptable).

### PR 11 — MQTT driver

- [ ] Not started

`…Scalar.Broker.Mqtt` on `MQTTnet` (`AddMqttConnection`); `broker-bindings.ts` learns `mqtt`
bindings; StreetlightsAPI example gains the console against test.mosquitto.org.

### PR 12 — AMQP driver

- [ ] Not started

`…Scalar.Broker.Amqp` on `RabbitMQ.Client` (`AddAmqpConnection`, exchange/routing-key-aware publish,
queue tail); `broker-bindings.ts` learns `amqp` bindings; small RabbitMQ example or Aspire addition.

**Optional follow-up (not planned):** `.Aspire` companion for the broker console — same CDN
standalone-bundle swap as `ScalarGrpcAspireExtensions`.

---

## Arazzo workflow support (PRs 13–18)

Full rationale, risks, and open questions in [ARAZZO-PROPOSAL.md](ARAZZO-PROPOSAL.md) — this section
just tracks checkboxes. Arazzo 1.1.0 added `AsyncAPI` as a first-class `sourceDescriptions` type, which
is what makes this relevant: this repo already owns both halves (AsyncAPI generation + protocol
transports) of the one workflow spec that can now describe HTTP *and* event steps in the same document.
This is a **1.1/2.0 theme, not a 1.0 blocker** — nothing in the core needed to change before the stable
tag.

Decisions already made (see ARAZZO-PROPOSAL.md §6 for the reasoning):
- Package IDs keep their spec names (`Bielu.Arazzo.NET` / `Bielu.Arazzo.NET.Readers` /
  `Bielu.AspNetCore.Arazzo` / `Bielu.Arazzo.Cli`), not renamed to any umbrella term.
- `Bielu.Arazzo`/`Bielu.Arazzo.Readers` are framework-free — **no dependency on anything else in `src/`**
  — and stay on the shared suite version (`net10.0` only, no independent version line, no polyfills).
- Two packages (model vs. readers), keeping `YamlDotNet` out of the model's dependency graph.
- Separate `dotnet arazzo` CLI tool, not verbs on `dotnet asyncapi`.
- No Scalar plugin for Arazzo — that's being contributed upstream to Scalar directly; our scope is
  interop only (serving documents at a discoverable URL + a thin config shim once upstream lands).

**Repo rename follow-through — done:**
- [x] Docs domain migration: `PackageProjectUrl` in `src/Directory.Build.props`, `docs/CNAME`, and the
  README docs link now point at `https://apidescriptions.bielu.pl/`. docfx now splits articles and API
  reference into AsyncAPI / Arazzo sections (`docs/toc.yml`, `docs/api/asyncapi/`, `docs/api/arazzo/`,
  `docs/articles/arazzo/`) with a two-column landing page (`docs/index.md`).
- [x] `RepositoryUrl` in `src/Directory.Build.props`, the README CI badge, `globalMetadata.repository`/
  `docurl` in `docs/docfx.json`, `CONTRIBUTING.md`'s upstream remote, `PACKAGE.md`, and the Scalar
  console asset READMEs now point at `bielu/Bielu.AspNetCore.ApiDescriptions` (confirmed via
  `gh repo view` — the GitHub repo **has** been renamed; the stale local `git remote -v` URL still
  redirects, which is what made this look unfinished).

### PR 13 — `Bielu.Arazzo.NET` + `.Readers` spec library

- [x] Merged — [#50](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/50)

Framework-free object model, writers, validation, and reader for the Arazzo Specification — a
structural clone of [ByteBard/AsyncAPI.NET](https://github.com/ByteBardOrg/AsyncAPI.NET)'s two-package
shape. NuGet has zero Arazzo packages today, so this is a from-scratch build, not a port.

- `Bielu.Arazzo`: ~16 model types (`ArazzoDocument`, `ArazzoWorkflow`, `ArazzoStep` incl. the
  AsyncAPI-specific `ChannelPath`/`Action`/`CorrelationId` fields new in 1.1, `ArazzoCriterion`,
  `ArazzoSelector`, etc.), `IArazzoSerializable`/`IArazzoWriter` abstraction with a shared
  `ArazzoJsonNodeWriter` tree-builder feeding both `ArazzoJsonWriter` and a hand-rolled
  `ArazzoYamlWriter` (no YamlDotNet dependency here), `Expressions/` runtime-expression parser +
  evaluator for the full §5.9 ABNF, `ArazzoValidator` (structural invariants: workflowId/stepId
  uniqueness, step target mutual-exclusivity, step action validity, JSON Schema shape check via
  `JsonSchema.Net`), `ArazzoWorkspace` skeleton with a pluggable `IArazzoSourceResolver` hook for PR 14.
- `Bielu.Arazzo.Readers`: JSON + YAML readers sharing one `ArazzoV1Deserializer` by converting YAML into
  the same `System.Text.Json.Nodes.JsonNode` tree JSON parses into (`YamlToJsonNodeConverter`) — same
  intent as ByteBard's unified `ParseNode` abstraction, achieved via the BCL's own node type instead of a
  bespoke one. Diagnostics (`ArazzoDiagnostics`/`ArazzoReaderError`) rather than exceptions.
- Deps used: `JsonPointer.Net`, `JsonSchema.Net` (model package); `YamlDotNet` (readers package only).
- Tests (`test/Bielu.Arazzo.Tests`): expression parser against the spec's own §5.9.1 examples table;
  JSON and YAML round-trip through a sample document exercising the AsyncAPI-specific step fields;
  validator unit tests. 30/30 passing; full `.slnx` builds with 0 new warnings.
- Found while building this: the spec is **not** ambiguous about AsyncAPI step semantics the way
  ARAZZO-PROPOSAL.md's risk R2 assumed — `channelPath`, `action` (`send`/`receive`), and `correlationId`
  are all precisely defined fixed fields. That risk is downgraded; PR 14's self-wiring resolver design
  gets simpler as a result.

### PR 14 — `Bielu.AspNetCore.Arazzo` (builder + self-wiring + cross-spec validation)

- [ ] Not started

`AddArazzo(...)` fluent builder mirroring the core AsyncAPI package's shape, `MapArazzo()` endpoint,
and — the differentiating feature — resolving `sourceDescriptions` against the *same app's* live
`IAsyncApiDocumentProvider` and OpenAPI documents so a step's `operationId`/`channelPath` reference is
validated at startup instead of failing in production. Depends on PR 13's `IArazzoSourceResolver`/
`ArazzoWorkspace` seam.

### PR 15 — `dotnet arazzo` CLI (`validate` / `lint` / `diff`)

- [ ] Not started

Separate tool (not verbs on `dotnet asyncapi`) so `arazzo` is independently discoverable on NuGet.
Reuses `Bielu.Arazzo.NET` internals and the existing hand-rolled `Commands/*Context.cs`/`*Worker.cs`
pattern from `Bielu.AspNetCore.AsyncApi.Cli`.

### PR 16 — `Bielu.Arazzo.Runtime` + HTTP transport (`arazzo run` / `test`)

- [ ] Not started

The step executor: expression evaluation, criterion checks, `onSuccess`/`onFailure`
(`end`/`retry`/`goto`), `dependsOn` join points, output propagation. HTTP transport built in; scope
explicitly excludes persistence, durable resumption, and scheduling — a test/automation runner, not a
workflow engine.

### PR 17 — Async transports for the runtime

- [ ] Not started

SignalR/gRPC first (existing protocol packages); Kafka/MQTT/AMQP once roadmap PRs 10–12 land — the
`IBrokerBridge` abstraction planned there is exactly the publish/await primitive an async Arazzo step
needs.

### PR 18 — Analyzers, source generator, template, docs, Scalar interop shim

- [ ] Not started

`BARAZZO0xx` analyzer rules, AOT source-generated metadata provider, `dotnet new asyncapi-arazzo`
template, docfx article, and the thin `ScalarOptions` shim for whatever config shape the upstream
Scalar Arazzo feature settles on (tracked separately, not built here).

---

## Order & rationale

PR 1 → 2 → 3 → 4 → 5 → 6 → 7 → 8 → 9 → 10 → 11 → 12.

Quick independent wins first; docs site after the CLI/analyzer/template surface exists so it can
document them; AOT after PRs 1–2 stabilize the metadata paths; broker proxy last (largest, new
npm/CI surface, and its console can reuse PR 2's message examples).

PR 13 → 14 → 15 → 16 → 17 → 18: the spec library first since it's fully independent (no dependency on
anything else in `src/`) and can run parallel to PRs 10–12; the builder/self-wiring and CLI next since
both only need the model; the runtime after that since it needs the CLI's `run`/`test` verbs to exist;
async transports depend on the runtime and reuse PRs 10–12's broker bridges once those land; analyzers
and docs last, once the surface they document is stable.

## Per-PR verification checklist

- `dotnet build` the `.slnx` + `dotnet test` (full suite); `npm run build` in touched asset workspaces.
- `.changeset/*.md` present (CI gate fails otherwise).
- Examples remain runnable (`StreetlightsAPI`, `SignalRChat`, `GrpcGreeter`, aspire).
- `README.md` + `AGENTS.md` package table updated when a package is added.
