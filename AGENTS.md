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
| `Bielu.AspNetCore.AsyncApi.Versioning` | API Versioning integration (`Asp.Versioning`) |
| `Bielu.AspNetCore.AsyncApi.Extensions.Protocols.SignalR` | SignalR protocol bindings |
| `Bielu.AspNetCore.AsyncApi.Extensions.Protocols.Grpc` | gRPC protocol bindings |
| `Bielu.AspNetCore.AsyncApi.Extensions.Protocols.Sse` | Server-Sent Events (SSE) protocol bindings |
| `Bielu.AspNetCore.AsyncApi.Extensions.Protocols.WebRtc` | WebRTC protocol bindings |
| `Bielu.AspNetCore.AsyncApi.Scalar` | Shared Scalar console-plugin infrastructure (+ private `@bielu/scalar-core` npm package) |
| `Bielu.AspNetCore.AsyncApi.Scalar.SignalR` (+ `.Aspire`) | Interactive SignalR console for Scalar (`@bielu/scalar-signalr` bundle) |
| `Bielu.AspNetCore.AsyncApi.Scalar.Grpc` (+ `.Aspire`) | Interactive gRPC-Web console for Scalar (`@bielu/scalar-grpc` bundle + protobuf descriptor endpoint) |
| `Bielu.Arazzo` (package id `Bielu.Arazzo.NET`) | Arazzo Specification object model, JSON/YAML writers, validation — framework-free, no dependency on the AsyncAPI packages |
| `Bielu.Arazzo.Readers` (package id `Bielu.Arazzo.NET.Readers`) | JSON/YAML readers for Arazzo documents, producing `Bielu.Arazzo` documents with diagnostics |
| `Bielu.AspNetCore.Arazzo` | ASP.NET Core integration for Arazzo: `AddArazzo`/`MapArazzo`, fluent workflow builder, self-wiring `sourceDescriptions` against this app's own live AsyncAPI/OpenAPI documents, startup cross-spec reference validation |
| `Bielu.Overlay` (package id `Bielu.Overlay.NET`) | OpenAPI Overlay Specification object model, validator, and apply engine (1.0.0 + 1.1.0). Operates on `JsonNode`, so overlays apply to OpenAPI, AsyncAPI, or Arazzo documents alike — framework-free. Non-OpenAPI targets are our extension, not spec-sanctioned: OAI closed [Overlay-Specification#268](https://github.com/OAI/Overlay-Specification/issues/268) `not planned`; we filed [#367](https://github.com/OAI/Overlay-Specification/issues/367) to revisit |
| `Bielu.Overlay.Readers` (package id `Bielu.Overlay.NET.Readers`) | JSON/YAML readers for Overlay documents, producing `Bielu.Overlay` documents with diagnostics |
| `Bielu.AspNetCore.AsyncApi.Overlay` | `AsyncApiOptions.AddOverlay(...)` — applies overlays at the serialization boundary, so `MapAsyncApi()` and build-time generation both serve an already-overlaid document. Also owns the spec-neutral `OverlayPipeline` the Arazzo package reuses |
| `Bielu.AspNetCore.Arazzo.Overlay` | `ArazzoOptions.AddOverlay(...)` — the same for `MapArazzo()`. Depends on `Bielu.AspNetCore.AsyncApi.Overlay` for the shared pipeline, which costs nothing since `Bielu.AspNetCore.Arazzo` already depends on the AsyncAPI core |
| `Bielu.Spec.Shared` | Document-parsing primitives shared by the spec libraries (YAML→`JsonNode` conversion), so the subtle plain-scalar type inference exists exactly once |
| `Bielu.Cli.Shared` | Shared console-CLI infrastructure (logging, argument parsing, file globbing, validate/diff report formatting) used by `dotnet-asyncapi`, `dotnet-arazzo`, and `dotnet-overlay` |
| `Bielu.Arazzo.Cli` (tool command `dotnet-arazzo`) | CLI tool for validating, linting, and diffing Arazzo documents |
| `Bielu.Overlay.Cli` (tool command `dotnet-overlay`) | CLI tool for applying overlays to, and validating, any JSON/YAML API description |
| `src/examples/StreetlightsAPI` | Minimal single-service example (best starting point) |
| `src/examples/SignalRChat` | SignalR example with the interactive Scalar console |
| `src/examples/GrpcGreeter` | gRPC example with the interactive Scalar console (gRPC-Web) |
| `src/examples/aspire` | Distributed .NET Aspire microservices demo |
| `src/examples/OverlayDemo` | Applies an OpenAPI Overlay to an AsyncAPI document and to an Arazzo one, showing map-keyed vs array-keyed targeting (console) |

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

- **Target framework:** `net10.0`. Requires the **.NET 10 SDK**.
- **Language settings (set per project):** `Nullable` enabled, `ImplicitUsings` enabled. Respect nullable annotations — don't suppress with `!` except in test arrange/assert as the existing tests do.
- **Central package management:** all package versions live in [`Directory.Packages.props`](./Directory.Packages.props). Reference packages with `<PackageReference Include="..." />` and **no `Version` attribute**. Add new versions to that file.
- **Shared MSBuild properties:** [`src/Directory.Build.props`](./src/Directory.Build.props) (authors, package metadata). **Version** is centralized in [`version.props`](./version.props) — do not hardcode versions in csproj files.
- **Public API tracking:** the core project uses `Microsoft.CodeAnalysis.PublicApiAnalyzers`. When you change the public surface, update `PublicAPI.Unshipped.txt`, or the build will fail.
- **`InternalsVisibleTo`:** core internals are exposed to `*.Tests`, `*.Merger`, and `*.Cli` — prefer `internal` over `public` for implementation details that only those consume.
- **Formatting:** governed by [`.editorconfig`](./.editorconfig) (4-space indent for C#, UTF-8 BOM, final newline). Run `dotnet format` before committing.
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

Target **70%+ line coverage** for code you add or change. Coverage is collected with `coverlet.collector`:

```bash
dotnet test --collect:"XPlat Code Coverage"
```

This writes Cobertura reports under each test project's `TestResults/`. New public behavior should ship with tests that exercise both the happy path and argument-validation/error paths (mirroring the existing `Unit/` and `Integration/` suites).

## Build, test, format

```bash
# Restore + build the whole solution
dotnet build src/Bielu.AspNetCore.AsyncApi.slnx

# Run all tests
dotnet test

# Run a single test project
dotnet test ./test/Bielu.AspNetCore.AsyncApi.Tests/Bielu.AspNetCore.AsyncApi.Tests.csproj

# Verify formatting (CI enforces this) / apply it
dotnet format --verify-no-changes src/Bielu.AspNetCore.AsyncApi.slnx
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
- Follow the **Arrange / Act / Assert** comment structure used throughout existing tests.
- Add or update tests for any behavior change. Match the naming pattern `Method_Scenario_ExpectedResult` (e.g. `AddServer_ThrowsForNullName`).

## Before opening a PR

1. `dotnet build` succeeds with no errors.
2. `dotnet test` passes.
3. `dotnet format --verify-no-changes src/Bielu.AspNetCore.AsyncApi.slnx` is clean.
4. Public-API changes are reflected in `PublicAPI.Unshipped.txt` and XML docs.
5. Update `README.md` / `CHANGELOG.md` when behavior or public APIs change.
6. Target the `main` branch. CI runs build, format check, and tests on every PR.