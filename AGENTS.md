# AGENTS.md

Guidance for AI coding agents working in this repository. Human contributors should also read [CONTRIBUTING.md](./CONTRIBUTING.md).

## What this project is

`Bielu.AspNetCore.AsyncApi` brings [AsyncAPI](https://www.asyncapi.com/) document generation to ASP.NET Core, mirroring the developer experience of `Microsoft.AspNetCore.OpenApi` (runtime + build-time generation, document/operation/schema transformers, protocol bindings, multiple documents). It builds on [ByteBard.AsyncAPI.NET](https://www.nuget.org/packages/ByteBard.AsyncAPI.NET/) for the AsyncAPI object model and serialization.

## Repository layout

The solution is `src/Bielu.AspNetCore.AsyncApi.slnx`. Source lives under `src/`, tests under `test/`.

| Project | Purpose |
|---------|---------|
| `Bielu.AspNetCore.AsyncApi` | Core library: services, extensions, schema generation, transformers |
| `Bielu.AspNetCore.AsyncApi.Attributes` | Attributes for declaring channels/operations/messages |
| `Bielu.AspNetCore.AsyncApi.Merger` | Merges multiple AsyncAPI documents (e.g. behind a gateway) |
| `Bielu.AspNetCore.AsyncApi.Cli` | CLI tool for getting/merging documents at build time |
| `Bielu.AspNetCore.AsyncApi.ApiDescription.Server` | MSBuild props/targets for build-time generation |
| `Bielu.AspNetCore.AsyncApi.SourceGenerators` | Source Generator for Native AOT and compile-time metadata |
| `Bielu.AspNetCore.AsyncApi.Analyzers` | Roslyn analyzers (`BASYNC*` rules). Not a package of its own: shipped inside `Bielu.AspNetCore.AsyncApi.Attributes`, whose analyzers reach apps through the core package. Rules are tracked in `AnalyzerReleases.*.md` |
| `Bielu.Analyzers.Shared` | Helpers shared by the AsyncAPI and Arazzo analyzers, compiled into each as linked source rather than referenced (no project file) |
| `Bielu.AspNetCore.AsyncApi.Avro` | Describes message payloads as Apache Avro schemas (`schemaFormat` `application/vnd.apache.avro`), converted from the generated JSON Schemas |
| `Bielu.AspNetCore.AsyncApi.OpenApiSchema` | Describes message payloads as OpenAPI 3.0 Schema Objects (`schemaFormat` `application/vnd.oai.openapi;version=3.0.0`), converted from the generated JSON Schemas |
| `Bielu.AspNetCore.AsyncApi.Protobuf` | Describes Protocol Buffers message payloads as `.proto` schemas (`schemaFormat` `application/vnd.google.protobuf`), rendered from the message descriptors |
| `Bielu.AspNetCore.AsyncApi.LikeC4` | Generates a LikeC4 architecture model (`.c4`) from AsyncAPI documents: services, channels matched by address, sends/receives, the service-to-service flows they imply, and views, plus a dynamic view per Arazzo workflow, and `LikeC4ModelChecker` for checking a hand-written model (its `likec4 export json`) against the documents. Framework-free; `dotnet asyncapi likec4` wraps it |
| `Bielu.AspNetCore.AsyncApi.CloudEvents` | Describes messages as CloudEvents 1.0: binary-mode `ce-*` headers per protocol binding, or the structured-mode `application/cloudevents+json` envelope |
| `Bielu.AspNetCore.AsyncApi.Versioning` | API Versioning integration (`Asp.Versioning`) |
| `Bielu.AspNetCore.AsyncApi.Wolverine` | Wolverine integration: builds channels and operations from Wolverine's message routing |
| `Bielu.AspNetCore.AsyncApi.Templates` | `dotnet new` template pack (`asyncapi-webapi`, `-signalr`, `-grpc`, `-console`, `-sln`) |
| `Bielu.AspNetCore.AsyncApi.Extensions.Protocols.SignalR` | SignalR protocol bindings |
| `Bielu.AspNetCore.AsyncApi.Extensions.Protocols.Grpc` | gRPC protocol bindings |
| `Bielu.AspNetCore.AsyncApi.Extensions.Protocols.Sse` | Server-Sent Events (SSE) protocol bindings |
| `Bielu.AspNetCore.AsyncApi.Extensions.Protocols.WebRtc` | WebRTC protocol bindings |
| `Bielu.AspNetCore.AsyncApi.Scalar` | Shared Scalar console-plugin infrastructure (+ private `@bielu/scalar-core` npm package) |
| `Bielu.AspNetCore.Scalar.Graph` | Not a .NET project: only `assets/`, the private `@bielu/scalar-graph` npm package of diagram primitives (edge paths, arrowheads, text fitting, pan/zoom, SVG/PNG export) the Arazzo and LikeC4 plugins share. Ships TypeScript source, inlined by each plugin's Vite build; no build step of its own |
| `Bielu.AspNetCore.AsyncApi.Scalar.SignalR` (+ `.Aspire`) | Interactive SignalR console for Scalar (`@bielu/scalar-signalr` bundle) |
| `Bielu.AspNetCore.AsyncApi.Scalar.Grpc` (+ `.Aspire`) | Interactive gRPC-Web console for Scalar (`@bielu/scalar-grpc` bundle + protobuf descriptor endpoint) |
| `Bielu.AspNetCore.AsyncApi.Scalar.Broker` | Interactive Kafka/MQTT/AMQP console for Scalar (`@bielu/scalar-broker` bundle + an opt-in server-side publish/tail bridge, since no browser speaks these protocols). No `.Aspire` companion: a CDN-loaded plugin would have no bridge to talk to |
| `Bielu.AspNetCore.AsyncApi.Scalar.Broker.Kafka` | Kafka driver (`IBrokerBridge` over `Confluent.Kafka`) for the broker console |
| `Bielu.Arazzo` (package id `Bielu.Arazzo.NET`) | Arazzo Specification object model, JSON/YAML writers, validation — framework-free, no dependency on the AsyncAPI packages |
| `Bielu.Arazzo.Readers` (package id `Bielu.Arazzo.NET.Readers`) | JSON/YAML readers for Arazzo documents, producing `Bielu.Arazzo` documents with diagnostics |
| `Bielu.AspNetCore.Arazzo` | ASP.NET Core integration for Arazzo: `AddArazzo`/`MapArazzo`, fluent workflow builder, self-wiring `sourceDescriptions` against this app's own live AsyncAPI/OpenAPI documents, startup cross-spec reference validation |
| `Bielu.AspNetCore.Arazzo.Analyzers` | Roslyn analyzers (`BARAZZO*` rules), packed into `Bielu.AspNetCore.Arazzo` rather than shipped on their own |
| `Bielu.Overlay` (package id `Bielu.Overlay.NET`) | OpenAPI Overlay Specification object model, validator, and apply engine (1.0.0 + 1.1.0). Operates on `JsonNode`, so overlays apply to OpenAPI, AsyncAPI, or Arazzo documents alike — framework-free. Non-OpenAPI targets are our extension, not spec-sanctioned: OAI closed [Overlay-Specification#268](https://github.com/OAI/Overlay-Specification/issues/268) `not planned`; we filed [#367](https://github.com/OAI/Overlay-Specification/issues/367) to revisit |
| `Bielu.Overlay.Readers` (package id `Bielu.Overlay.NET.Readers`) | JSON/YAML readers for Overlay documents, producing `Bielu.Overlay` documents with diagnostics |
| `Bielu.AspNetCore.AsyncApi.Overlay` | `AsyncApiOptions.AddOverlay(...)` — applies overlays at the serialization boundary, so `MapAsyncApi()` and build-time generation both serve an already-overlaid document. Also owns the spec-neutral `OverlayPipeline` the Arazzo package reuses |
| `Bielu.AspNetCore.Arazzo.Overlay` | `ArazzoOptions.AddOverlay(...)` — the same for `MapArazzo()`. Depends on `Bielu.AspNetCore.AsyncApi.Overlay` for the shared pipeline, which costs nothing since `Bielu.AspNetCore.Arazzo` already depends on the AsyncAPI core |
| `Bielu.AspNetCore.Arazzo.Scalar` (+ `.Aspire`) | Arazzo document type for Scalar (`@bielu/scalar-arazzo` bundle, on Scalar's plugin `documentTypes` from `@scalar/api-reference` 1.80.0): `AddArazzoDocument`/`WithArazzo`/`MapScalarArazzoAssets`. Reuses `@bielu/scalar-core`'s element registration and `installScalarPluginHook`; helper logic is unit-tested with vitest in `assets/test`. `.Aspire` loads the CDN-pinned `dist/scalar-plugin.mjs` into a Scalar.Aspire resource (`WithArazzo`) and adds Arazzo sources (`AddArazzoDocument`) |
| `Bielu.AspNetCore.ApiDescriptions` | Build-time generation shared by every document family: the single `IDocumentProvider` `dotnet getdocument` queries, composition with `Microsoft.AspNetCore.OpenApi`, and file-name settlement. Packages plug in a public `IBuildTimeDocumentSource` via `AddBuildTimeDocumentSource<T>()`; AsyncAPI is one such source |
| `Bielu.Spec.Shared` | Document-parsing primitives shared by the spec libraries (YAML→`JsonNode` conversion), so the subtle plain-scalar type inference exists exactly once |
| `Bielu.Cli.Shared` | Shared console-CLI infrastructure (logging, argument parsing, file globbing, validate/diff report formatting) used by `dotnet-asyncapi`, `dotnet-arazzo`, and `dotnet-overlay` |
| `Bielu.AspNetCore.ApiCatalog` | Serves an RFC 9727 API catalog at `/.well-known/api-catalog`: a Linkset of every API description the app serves (AsyncAPI, OpenAPI, Arazzo), found from the app's own endpoints |
| `Bielu.AspNetCore.XRegistry` | Serves the messages and payload schemas of the app's AsyncAPI documents as a read-only CNCF xRegistry (1.0-rc4) |
| `Bielu.AspNetCore.ApiDescriptions.Mcp` | Experimental. Exposes the app's API descriptions to AI agents through a Model Context Protocol server: documents as resources, read-only lookup tools |
| `Bielu.AspNetCore.AsyncApi.Tests.ExternalLibraries`, `.Tests.Shared.LibraryChannels` | Test projects (under `src/` for historical reasons): `[AsyncApi]` channels declared in a separate library assembly, and the test that the generator finds them |
| `Bielu.Arazzo.Cli` (tool command `dotnet-arazzo`) | CLI tool for validating, linting, and diffing Arazzo documents |
| `Bielu.Overlay.Cli` (tool command `dotnet-overlay`) | CLI tool for applying overlays to, and validating, any JSON/YAML API description |
| `src/examples/StreetlightsAPI` | Minimal single-service example (best starting point) |
| `src/examples/SignalRChat` | SignalR example with the interactive Scalar console |
| `src/examples/GrpcGreeter` | gRPC example with the interactive Scalar console (gRPC-Web) |
| `src/examples/aspire` | Distributed .NET Aspire microservices demo |
| `src/examples/OverlayDemo` | Applies an OpenAPI Overlay to an AsyncAPI document and to an Arazzo one, showing map-keyed vs array-keyed targeting (console) |
| `src/examples/ArazzoScalar` | Arazzo workflows rendered in the Scalar API Reference next to the OpenAPI and AsyncAPI documents their steps call (`Bielu.AspNetCore.Arazzo.Scalar`) |
| `src/examples/AotStreetlights` | `PublishAot` example using the source-generated metadata provider; `scripts/verify-aot.sh` publishes it and compares its document against the reflection-based build |
| `src/examples/BuildTimeDocuments` | One `dotnet getdocument` run writing the OpenAPI, AsyncAPI and Arazzo documents of one app; `scripts/verify-build-time-documents.sh` builds it and proves a stale Arazzo reference fails the build |

## Feature slices & layering

The service applications (see the Aspire example) are organized as **vertical feature slices**, not by technical type at the top level. Each business capability is a self-contained folder under `Features/`, and each slice is **layered internally** by responsibility. Follow this when adding code to a service:

```
Features/
  Inventory/                      # one vertical slice = one capability
    InventoryController.cs        # entry point (Controller / minimal API / Hub) at slice root
    Models/                       # domain/data models for this slice
      InventoryItem.cs
    Events/                       # messages/events published or consumed (AsyncAPI surface)
      OrderCreatedEvent.cs
      StockLevelChangedEvent.cs
    Services/                     # application/business logic, abstraction + impl
      IInventoryManagementService.cs
      InventoryManagementService.cs
      CachedInventoryManagementService.cs   # decorator (Scrutor) over the impl
    Data/                         # persistence (EF Core DbContext, repositories)
      InventoryDbContext.cs
    Diagnostics/                  # metrics/tracing for this slice
      InventoryMetrics.cs
```

Rules of thumb:

- **One slice per capability.** Add a new `Features/<Capability>/` folder rather than scattering files into shared technical folders. A change to one capability should touch one slice.
- **Layer inside the slice** with the standard subfolders above (`Models`, `Events`, `Services`, `Data`, `Diagnostics`, `Hubs`). Keep the entry point (controller/hub/worker) at the slice root.
- **Depend inward.** Entry point → `Services` (abstractions) → `Data`. The entry point must not reach into another slice's internals; cross-slice communication goes through **events/messages** (`Events/`) or a shared abstraction.
- **Decorators for cross-cutting concerns within a slice** (e.g. caching) are registered with Scrutor and named `Cached…`/`…Decorator`, wrapping the interface — see `CachedInventoryManagementService`.
- **Truly cross-cutting, app-wide concerns** (caching primitives, messaging/event publishing, telemetry, service discovery) live in the shared **`ServiceDefaults`** project under `Caching/`, `Messaging/`, `Diagnostics/` — not inside a feature slice. Reuse those abstractions (`ICacheService`, `IEventPublisher`) instead of re-implementing per slice.

## Conventions

- **Target frameworks:** runtime libraries and their tests target `$(BieluTargetFrameworks)` (`net10.0;net11.0`, defined in the root [`Directory.Build.props`](./Directory.Build.props)); use `<TargetFrameworks>$(BieluTargetFrameworks)</TargetFrameworks>` in a new library or test project rather than listing frameworks. Analyzers and the source generator stay `netstandard2.0`. Building needs the **.NET 11 SDK** (pinned in [`global.json`](./global.json)); running the `net10.0` tests also needs the **.NET 10 runtime**. Packages versioned with ASP.NET Core get a per-framework version in `Directory.Packages.props`. Examples are single-framework apps on `$(BieluExampleTargetFramework)` (default `net10.0`; `-p:BieluExampleTargetFramework=net11.0` switches one), and the CLI tools ship both frameworks: `scripts/verify-cli-runtimes.sh` checks `getdocument` across them. See [Supported .NET versions](./docs/articles/supported-dotnet-versions.md).
- **Language settings (set per project):** `Nullable` enabled, `ImplicitUsings` enabled. Respect nullable annotations — don't suppress with `!` except in test arrange/assert as the existing tests do.
- **Central package management:** all package versions live in [`Directory.Packages.props`](./Directory.Packages.props). Reference packages with `<PackageReference Include="..." />` and **no `Version` attribute**. Add new versions to that file.
- **Shared MSBuild properties:** [`src/Directory.Build.props`](./src/Directory.Build.props) (authors, package metadata). **Version** is centralized in [`version.props`](./version.props) — do not hardcode versions in csproj files.
- **Public API tracking:** projects that have a `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt` next to their csproj (every packable library; not the analyzers, source generator, CLI tools or templates) are checked by `Microsoft.CodeAnalysis.PublicApiAnalyzers`; `src/Directory.Build.props` turns it on for any project with those files. When you change the public surface, update `PublicAPI.Unshipped.txt`, or the build fails (RS0016/RS0017). A new library project starts with both files holding only `#nullable enable`; `dotnet format analyzers <csproj> --diagnostics RS0016` fills Unshipped. Never edit `PublicAPI.Shipped.txt` by hand: `scripts/ship-public-api.mjs` moves Unshipped into Shipped as part of `npm run version` when a stable version is released.
- **`InternalsVisibleTo`:** core internals are exposed to `*.Tests`, `*.Merger`, and `*.Cli` — prefer `internal` over `public` for implementation details that only those consume.
- **Formatting:** governed by [`.editorconfig`](./.editorconfig) (4-space indent for C#, UTF-8 BOM, final newline). Run `dotnet format` before committing.
- **Vue formatting:** the Scalar plugins' `.vue` components use Scalar's own Prettier setup ([`.prettierrc`](./.prettierrc): `prettier-plugin-tailwindcss` class order, sorted imports, one attribute per line). Run `npm run format:vue`; CI runs `npm run format:vue:check`. TypeScript and CSS are not Prettier-formatted, as in Scalar (which uses Biome for them). Vue drops whitespace between two elements once it contains a newline, so a space that must render between inline elements is written as `{{ ' ' }}`, never left to the line layout. CI runs `npm run check:vue-whitespace`, which fails when a changed template renders a different number of spaces between elements than its base.
- **Style idioms** (follow the surrounding code):
  - Public APIs get XML doc comments, often with `<example>`/`<code>` blocks.
  - Validate arguments with `ArgumentNullException.ThrowIfNull(...)`.
  - Configuration uses fluent, chainable extension methods returning the same object (see `AsyncApiOptions`).
  - File-scoped namespaces.
- Some example files (e.g. `StreetlightsAPI/Program.cs`) intentionally use the older `Startup`/block-namespace style — match the file you're editing, not a single global style.

## Engineering principles

Hold new and changed code to these principles — they are the bar for review:

- **SOLID:**
  - *Single responsibility* — each service/transformer/extension class does one thing (see how `AsyncApiDocumentService`, `AsyncApiJsonSchemaService`, and the document/operation/schema transformers are separated).
  - *Open/closed* — extend behavior through the transformer pipeline and options delegates rather than editing core generation logic.
  - *Liskov & interface segregation* — depend on the small interfaces (`IAsyncApiDocumentProvider`, `IAsyncApiDocumentTransformer`, `IAsyncApiOperationTransformer`, `IAsyncApiSchemaTransformer`, `IDocumentProvider`); keep interfaces focused.
  - *Dependency inversion* — register and consume abstractions via DI (`IServiceCollection` extensions, keyed services), never `new` up a service that has a registered interface.
- **DRY** — reuse existing helpers/extensions (`Extensions/`, `Helpers/`, `AsyncApiNamingHelper`) instead of duplicating logic. Shared MSBuild/package config already lives in `Directory.*.props` and `version.props`; don't repeat it per project.
- **KISS** — prefer the simplest solution that fits the existing fluent/extension-method style. Avoid speculative generality and unnecessary indirection.
- **Proper abstraction** — introduce a new interface or transformer only when there's a real seam; mark implementation details `internal` (the `InternalsVisibleTo` setup already grants tests/Merger/Cli access). Don't widen the public API surface without need (it's tracked by the analyzer).

## Code coverage

Total line coverage must stay at or above **85%**, and code you add or change should be covered at least that well. CI enforces the total: the PR build merges every test project's report with ReportGenerator and fails below 85% (`minimum-line-coverage` in [`buildAndPublishPackage.yml`](./.github/workflows/buildAndPublishPackage.yml)). The minimum only moves up: raise it when the total has comfortably passed the next step, and never lower it to land a change; add tests instead, or mark genuinely untestable code (process entry points, hosting glue) `[ExcludeFromCodeCoverage]` with a comment saying why.

To see the number CI will see, run `mise run coverage`: it runs the suite and prints the merged summary (HTML report in `artifacts/coverage/report/`). Coverage is collected with `coverlet.collector`:

```bash
dotnet test --collect:"XPlat Code Coverage"
```

This writes Cobertura reports under each test project's `TestResults/`. A plain `dotnet test` collects them too: [`coverlet.runsettings`](./coverlet.runsettings), applied to every test project via `Directory.Build.props`, enables the collector. In it, only `Bielu.*` library assemblies are measured, and generated sources (`obj/`), examples and test helper assemblies are excluded. Every test project must reference `coverlet.collector`, or its packages silently drop out of the report. New public behavior should ship with tests that exercise both the happy path and argument-validation/error paths (mirroring the existing `Unit/` and `Integration/` suites).

## Build, test, format

The toolchain is pinned in [`mise.toml`](./mise.toml): `mise install` provisions the .NET SDK and Node.js, and `mise run build|test|format|format-check` wrap the commands below.

```bash
# Restore + build the whole solution
dotnet build src/Bielu.AspNetCore.AsyncApi.slnx

# Run all tests
dotnet test

# Run a single test project
dotnet test ./test/Bielu.AspNetCore.AsyncApi.Tests/Bielu.AspNetCore.AsyncApi.Tests.csproj

# Verify formatting / apply it. IL2026 and IL3050 are excluded because the trim annotations on the
# reflection-based default path are not settled yet; scripts/verify-aot.sh is the place to enforce them once they are.
dotnet format --verify-no-changes src/Bielu.AspNetCore.AsyncApi.slnx --exclude-diagnostics IL2026 IL3050
dotnet format src/Bielu.AspNetCore.AsyncApi.slnx
```


Run the sample to sanity-check end to end:

```bash
cd src/examples/StreetlightsAPI && dotnet run
# AsyncAPI JSON: http://localhost:5000/asyncapi/v1.json
# Scalar UI:     http://localhost:5000/scalar
```

## Testing conventions

- **xUnit** + **Shouldly** assertions + **NSubstitute** for mocking. ASP.NET integration tests use `Microsoft.AspNetCore.Mvc.Testing`; UI tests use `Microsoft.Playwright`.
- Tests are split into `Unit/` and `Integration/` folders; shared inputs live in `Fixtures/`.
- Tests that need real infrastructure (e.g. a Kafka broker) start it with Aspire's `DistributedApplicationTestingBuilder` while the code under test runs in-process, so coverage still counts. Mark them `[DockerFact]` so they skip, rather than fail, without a running Docker daemon. See `test/Bielu.AspNetCore.AsyncApi.Scalar.Aspire.Tests`.
- Follow the **Arrange / Act / Assert** comment structure used throughout existing tests.
- `dotnet test` runs each multi-targeted test project once per framework. `FrameworkParityDocumentTests` compares the generated document against golden files under `Fixtures/Golden/` on both, so a runtime difference in schema generation fails the build; regenerate them with `BIELU_UPDATE_GOLDEN=1 dotnet test -f net10.0` only for an intended change.
- Add or update tests for any behavior change. Match the naming pattern `Method_Scenario_ExpectedResult` (e.g. `AddServer_ThrowsForNullName`).

## Before opening a PR

1. `dotnet build` succeeds with no errors.
2. `dotnet test` passes.
3. `dotnet format --verify-no-changes src/Bielu.AspNetCore.AsyncApi.slnx --exclude-diagnostics IL2026 IL3050` is clean, and so is `npm run format:vue:check`.
4. Public-API changes are reflected in `PublicAPI.Unshipped.txt` and XML docs.
5. Update `README.md` / `CHANGELOG.md` when behavior or public APIs change.
6. Target the `main` branch. CI runs build, format check, and tests on every PR.