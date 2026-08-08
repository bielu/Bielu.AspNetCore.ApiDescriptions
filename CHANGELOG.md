# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and from now on the
`[Unreleased]` section and released version entries are managed with
[changesets](https://github.com/changesets/changesets) — see [`.changeset/README.md`](.changeset/README.md).

This project is a fork/evolution of [Saunter](https://github.com/asyncapi/saunter), the original AsyncAPI documentation generator for .NET. Below you'll find both the version history for Bielu.AspNetCore.AsyncApi and a comparison of changes from the original Saunter library.

> **Versioning note.** No stable release has shipped yet. Every NuGet package in this repository
> shares a single version (`1.0.0`, in `version.props`) and has so far only been published to the
> **`beta` pre-release channel** on NuGet.org (`1.0.0-beta.*`). The
> [Pre-release channel history](#pre-release-channel-history) below reconstructs, from the published
> packages, when each package and capability first became available on that channel. Everything under
> `[Unreleased]` is targeting the first stable `1.0.0`.

## [Unreleased]

### Added

- **Documentation site** - New documentation site built with docfx, hosted on GitHub Pages. Provides comprehensive guides for attributes, configuration, protocols, and CLI usage.
- **Roslyn analyzers** - New `Bielu.AspNetCore.AsyncApi.Analyzers` package (bundled with the Attributes package) that provides compile-time diagnostics for common AsyncAPI attribute misuses. Includes checks for missing `[AsyncApi]` attributes, operations without channels, duplicate names, and invalid payload types.
- **CLI `validate` command** - New `dotnet asyncapi validate` command to validate AsyncAPI documents against the spec. Supports globbing, strict mode (warnings as errors), and JSON output for CI pipelines.
- **CLI `diff` command** - New `dotnet asyncapi diff` command to compare two AsyncAPI documents. Detects breaking changes (removals, narrowing) and non-breaking changes (additions). Supports text, JSON, and Markdown reports.
- **XML documentation support** - Automatic population of channel, operation, message and schema descriptions from C# XML documentation comments (`/// <summary>`, `/// <remarks>`). Use `options.IncludeXmlComments()` to register documentation sources.
- **Message examples** - Support for embedding examples in AsyncAPI messages via `[MessageExample]` attribute or fluent `options.AddMessageExample()`. Scalar and protocol consoles can use these to prefill request editors.
- **Interactive SignalR console for Scalar** - New `Bielu.AspNetCore.AsyncApi.Scalar.SignalR` package
  (ASP.NET Core) and `Bielu.AspNetCore.AsyncApi.Scalar.SignalR.Aspire` package (Aspire hosting) that
  add a live SignalR client panel to the Scalar API Reference. The panel reads the SignalR bindings
  from your AsyncAPI document(s) and lets you connect to a hub, invoke client-to-server methods and
  watch server-to-client events. Both are powered by the standalone, npm-publishable
  `@bielu/scalar-signalr` bundle, integrated two ways: the ASP.NET Core package serves a companion
  `plugin.js` that registers the console as a plugin alongside Scalar's own bundle — call
  `MapScalarSignalRAssets()` to serve it and `options.WithSignalRClient(...)` to inject the script —
  while the Aspire extension swaps the Scalar container's bundle URL for the `@bielu/scalar-signalr`
  drop-in. Wired into the `SignalRChat` example. The protocol-agnostic half (document discovery,
  auth-state capture, schema examples, the embedded-bundle endpoint and Scalar HeadContent injection,
  plus the shared npm build/embed MSBuild targets) lives in a common `Bielu.AspNetCore.AsyncApi.Scalar`
  package and its private `@bielu/scalar-core` npm package, so further protocol consoles (gRPC, ...)
  reuse it rather than copying it. *(Not yet published to any channel.)*
- **Interactive gRPC console for Scalar** - New `Bielu.AspNetCore.AsyncApi.Scalar.Grpc` package
  (ASP.NET Core) and `Bielu.AspNetCore.AsyncApi.Scalar.Grpc.Aspire` package (Aspire hosting) that add
  a live gRPC client panel to the Scalar API Reference, mirroring the SignalR console on the shared
  `Bielu.AspNetCore.AsyncApi.Scalar` / `@bielu/scalar-core` foundation. The panel reads the `grpc`
  bindings from your AsyncAPI document(s), groups RPC methods by service, prefills a JSON request
  editor from the payload schema and invokes **unary and server-streaming** methods over **gRPC-Web**
  (`@bufbuild/protobuf` + `@connectrpc/connect-web`; client-/bidi-streaming methods render as
  documentation with a "not invokable from the browser" badge). Because AsyncAPI payload schemas carry
  no protobuf field numbers, `MapScalarGrpcAssets()` also serves the real protobuf descriptors of every
  mapped gRPC service at `{assetsPath}/descriptors` (a serialized `FileDescriptorSet`), which the
  console uses to encode wire messages dynamically. Call `MapScalarGrpcAssets()` to serve the
  `@bielu/scalar-grpc` bundle + descriptors and `options.WithGrpcClient(...)` to inject the script;
  the target app must enable gRPC-Web (`Grpc.AspNetCore.Web`, `UseGrpcWeb`). Scalar auth passes
  through as gRPC-Web metadata (plain HTTP headers). Wired into the `GrpcGreeter` example.
  *(Not yet published to any channel.)*
- **`dotnet new` template pack** - New `Bielu.AspNetCore.AsyncApi.Templates` package providing 5 templates (`asyncapi-webapi`, `asyncapi-signalr`, `asyncapi-grpc`, `asyncapi-console`, `asyncapi-sln`) to quickly bootstrap AsyncAPI-enabled projects. Includes interactive consoles and multi-project solution support.
- **Server-Sent Events (SSE) protocol bindings** - New `Bielu.AspNetCore.AsyncApi.Extensions.Protocols.Sse`
  package providing a custom `sse` protocol with channel, operation, message and server bindings
  modelling the `text/event-stream` (`event`/`id`/`retry`/`data`) wire shape. *(Not yet published to any channel.)*
- **WebRTC protocol bindings** - New `Bielu.AspNetCore.AsyncApi.Extensions.Protocols.WebRtc` package
  providing a custom `webrtc` protocol with channel, operation, message and server bindings covering
  `RTCDataChannel` streams and SDP/ICE signaling. *(Not yet published to any channel.)*
- **Changeset-driven release workflow** - Contributor changes are now recorded as
  [changesets](https://github.com/changesets/changesets); the shared NuGet version and this changelog
  are updated from them via `scripts/apply-nuget-version.mjs`.

### Removed

- **`Bielu.AspNetCore.AsyncApi.UI`** - The obsolete built-in UI package has been removed. Use `Scalar.AspNetCore` instead.

### Fixed

- `BindingsRef` on `[Channel]` and operation attributes now actually attaches the referenced binding
  (registered via `AddChannelBinding`/`AddOperationBinding`) to the channel/operation in the generated
  document. Previously the binding was only stored under `components` and never linked.

## [1.0.1] - 2026-08-08

### Patch Changes

- [#65](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/65) [`e41bdcb`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/e41bdcb39591813f63497f6157c1d1bedb28651e) Thanks [@AmmonRoberts](https://github.com/AmmonRoberts)! - Removed BASYNC009 analysis rule from AsyncApiAttribute.

## [1.0.0] - 2026-08-02

### Major Changes

- [#60](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/60) [`6e0fece`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/6e0fecec3f52e45521931fc808723478fb23a0bc) Thanks [@bielu](https://github.com/bielu)! - First stable release.

  Everything in this changelog shipped only to the `1.0.0-beta.*` pre-release channel until now. The
  public API of `Bielu.AspNetCore.AsyncApi` is baselined at this version
  (`PublicAPI.Shipped.txt`), and the `BASYNC001`–`BASYNC009` analyzer rules move from unshipped to
  `Release 1.0.0`, so from here on an accidental break in either surface is caught at build time rather
  than discovered by a consumer.

  This entry also exists to pin the version arithmetic. The shared-version placeholder
  (`build/changeset/nuget-suite`) is held at `0.0.0` and this major bump lands it exactly on `1.0.0` —
  without it the accumulated minor changesets would have produced `1.1.0` as the first stable version,
  which the `dotnet new` templates (which pin `Version="1.0.0"`), the documentation and the existing
  `1.0.0-beta.*` channel all contradict.

### Minor Changes

- [#52](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/52) [`1dc09b6`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/1dc09b65075fabf81f2f0e0b3e6a3d3caf656832) Thanks [@bielu](https://github.com/bielu)! - Added `Bielu.AspNetCore.Arazzo`, the ASP.NET Core integration for the Arazzo workflow spec library added in the previous release. Mirrors the core AsyncAPI package's shape: `AddArazzo`/`MapArazzo`, a fluent workflow/step builder (`AddWorkflow`, `Step`, `Channel`, `OperationPath`, `Operation`, `SuccessCriteria`, `Output`, ...), and — the differentiating feature — self-wiring `sourceDescriptions` against this same app's own live AsyncAPI (`AddAsyncApiSource`) and OpenAPI (`AddOpenApiSource`) documents via new `OpenApiSourceResolver`/`AsyncApiSourceResolver` implementations of PR13's `IArazzoSourceResolver`. By default, every workflow step's `operationId`/`operationPath`/`channelPath` is resolved against those live documents once at app startup (`ArazzoOptions.ValidateSourceReferencesOnStartup`), so a renamed channel or operation fails startup instead of failing in production — implemented as an `IStartupFilter` rather than an `IHostedService`, since endpoint/ApiDescription data isn't available yet when user-registered hosted services run. `ArazzoWorkspace` (`Bielu.Arazzo`) also gained `TryResolveOperationPath`/`TryResolveChannelPath` convenience methods to support this.

  Workflows and steps can also be identified by a marker type rather than a string id, so cross-references move with a rename instead of silently dangling: `AddWorkflow<T>()`, `Step<T>()`, `DependsOn<T>()` (on both workflows and steps), and `Workflow<T>()`. The mapping is `ArazzoId.FromType<T>()` — the type name camel-cased, so `MeasureAndAlert` becomes `measureAndAlert` — which keeps the emitted document idiomatic and lets the string and generic forms refer to the same workflow, so the generic form can be adopted incrementally.

- [#54](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/54) [`9037c9b`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/9037c9bf2594f18ae13cc3686085504d6bded118) Thanks [@bielu](https://github.com/bielu)! - Added `Bielu.Arazzo.Cli` (tool command `dotnet-arazzo`), a separate CLI tool for Arazzo workflow documents with `validate`, `lint`, and `diff` commands — kept separate from `dotnet asyncapi` so `arazzo` stays independently discoverable on NuGet. `validate` runs the reader diagnostics plus `ArazzoValidator`'s structural invariants. `lint` is new: style and graph-shape checks beyond structural validation — missing summaries/descriptions, identifiers with characters that don't travel well across tooling, circular `dependsOn` graphs (step- and workflow-level), dangling same-document `dependsOn` references, and `components` entries that are declared but never referenced. `diff` compares two documents and classifies added/removed workflows and steps, step target/action changes, and source-description changes as breaking or non-breaking, with text/JSON/markdown output and `--fail-on-breaking`.

  Also extracted `Bielu.Cli.Shared`, the console-CLI infrastructure (`ConsoleCliLogger`, `CliArgumentReader`, `CliFileResolver`, and the `validate`/`diff` report renderers) that the CLI tools would otherwise have duplicated. `Bielu.AspNetCore.AsyncApi.Cli` now builds on the same shared package; its behavior and tests are unchanged, and `Bielu.Overlay.Cli` (below) is built on it from the start.

- [#50](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/50) [`5605a01`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/5605a01d15ecb568fe238453bc2034b111930115) Thanks [@bielu](https://github.com/bielu)! - Added `Bielu.Arazzo.NET` and `Bielu.Arazzo.NET.Readers`, a framework-free object model, JSON/YAML writers, validation, and readers for the [Arazzo Specification](https://spec.openapis.org/arazzo/latest.html) (OpenAPI workflows). Arazzo 1.1.0 added `AsyncAPI` as a first-class `sourceDescriptions` type, letting a workflow describe steps against event channels (`channelPath`, `action: send|receive`, `correlationId`) alongside HTTP operations — this is the first PR of a multi-PR effort to bring Arazzo workflow support to the suite; see `ARAZZO-PROPOSAL.md` for the full plan. Includes a runtime-expression parser/evaluator for the spec's `$inputs`/`$outputs`/`$steps`/`$workflows`/`$sourceDescriptions`/`$components` expressions.

- [#48](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/48) [`09530ad`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/09530ad6af4ea97b7e6650a5741097797398bd0e) Thanks [@bielu](https://github.com/bielu)! - Add `validate` and `diff` commands to the `dotnet-asyncapi` CLI tool.
  The `validate` command allows validating one or more AsyncAPI documents against the specification, with support for glob patterns and strict mode.
  The `diff` command allows comparing two AsyncAPI documents and identifying breaking and non-breaking changes between them, with report support in text, JSON, and Markdown formats.

- [#44](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/44) [`4007c0d`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/4007c0df70afdee596844ef2c588cb304a64d5f2) Thanks [@bielu](https://github.com/bielu)! - deprecate `Bielu.AspNetCore.AsyncApi.UI` in favour of Scalar: `MapAsyncApiUi()` is now `[Obsolete]` and the package description marks it deprecated. Render the generated document with `Scalar.AspNetCore`'s `MapScalarApiReference(options => options.AddAsyncApiDocument(...))` instead (the `MapAsyncApi()` document endpoint is unchanged), optionally with `Bielu.AspNetCore.AsyncApi.Scalar.SignalR` / `.Scalar.Grpc` for interactive protocol consoles. All in-repo examples (StreetlightsAPI, SignalRChat, GrpcGreeter, Aspire Mini Shop) were migrated off the built-in UI to Scalar.

- [#45](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/45) [`2519a39`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/2519a391f8caa6987cee3146a39a026999848bcd) Thanks [@bielu](https://github.com/bielu)! - add an interactive gRPC console for Scalar: new `Bielu.AspNetCore.AsyncApi.Scalar.Grpc` (ASP.NET Core) and `Bielu.AspNetCore.AsyncApi.Scalar.Grpc.Aspire` (Aspire hosting) packages, powered by the new `@bielu/scalar-grpc` npm bundle on the shared `Bielu.AspNetCore.AsyncApi.Scalar` / `@bielu/scalar-core` foundation. `MapScalarGrpcAssets()` serves the console bundle plus a protobuf descriptor endpoint (`{assetsPath}/descriptors`, a serialized `FileDescriptorSet` gathered from the mapped gRPC services) and `options.WithGrpcClient(...)` injects the console into Scalar. The console parses `grpc` AsyncAPI bindings, prefills JSON requests from payload schemas and invokes unary + server-streaming methods over gRPC-Web with Scalar auth passed through as call metadata; client-/bidi-streaming methods are documentation-only. Wired into the `GrpcGreeter` example (`UseGrpcWeb(DefaultEnabled = true)`).

- [#48](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/48) [`1315fc8`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/1315fc8302ca16bad8c42cfe307fea2e8a6c564f) Thanks [@bielu](https://github.com/bielu)! - Add support for embedding examples in AsyncAPI messages.
  Includes the `[MessageExample]` attribute for declarative example definition on message types and the `AddMessageExample<T>()` fluent configuration on `AsyncApiOptions`.
  Examples can be provided as JSON literals, via provider types, or as object instances.
  Added the `SetSchemaExampleFromMessageExample` option to automatically promote the first message example to its associated JSON schema.

- [#49](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/49) [`7c9623b`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/7c9623b4daf82faf5cf3ed79fbd64198ae9b5842) Thanks [@bielu](https://github.com/bielu)! - Added support for Native AOT (Ahead-of-Time) compilation. Includes a new Roslyn-based Source Generator (`Bielu.AspNetCore.AsyncApi.SourceGenerators`) that discovers `[AsyncApi]`/`[Channel]` attributes at compile time and generates metadata to avoid reflection at runtime, plus a parameterless `AddAsyncApiGeneratedMetadata()` overload for the default `v1` document. See the new [Native AOT Support](https://apidescriptions.bielu.pl/articles/native-aot.html) guide for setup instructions, including `JsonSerializerContext` configuration.

- [#54](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/54) [`8a1ca45`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/8a1ca45a76043075de4b2b2d9137f7c4ef0cf649) Thanks [@bielu](https://github.com/bielu)! - Added `Bielu.Overlay.Cli` (tool command `dotnet-overlay`), a CLI for the OpenAPI Overlay support added alongside it. `apply` transforms a description with one or more overlays; `validate` checks overlay documents on their own terms, without a target description in hand, which is what makes it usable as a CI gate on the overlays themselves.

  Repeated `--overlay` arguments are applied **in the order given**, each against the result of the last — the same sequencing the specification requires of actions within a single overlay — so `--overlay strip-internal.yaml --overlay add-metadata.yaml` strips first and then annotates what survived. Nothing is written when application reports errors, so a partially transformed document is never left for a later build step to pick up. `--strict` promotes a `target` that matches zero nodes from a warning to an error: the specification permits zero matches, but in a pipeline that almost always means the overlay has drifted out of sync with the description, and failing the build beats silently publishing an untransformed document.

  Since the engine works on a `JsonNode` tree rather than a typed object model, `--file` accepts OpenAPI, AsyncAPI, or Arazzo descriptions in either JSON or YAML. Output format follows `--format` when given, otherwise the `--output` extension (`.yaml`/`.yml` → YAML) as `dotnet asyncapi merge` already does, and defaults to JSON when writing to standard output so the command pipes cleanly.

  Supporting that round-trip, `Bielu.Spec.Shared` gained `JsonNodeToYamlConverter`, the inverse of its existing `YamlToJsonNodeConverter` — without it a YAML description could only ever be written back out as JSON. Strings that would otherwise read back as another type are quoted, which matters for values like Arazzo's `channelPath` (`{$sourceDescriptions.events.url}#/channels/...`), where an unquoted leading `{` would parse as a YAML flow mapping.

  Fixed while building it: `YamlToJsonNodeConverter` did not treat an **empty plain scalar** as null, so a YAML field written `description:` with no value was read as an empty string rather than null. Quoted `""` was, and remains, an empty string. This also affects `Bielu.Arazzo.NET.Readers`, which shares the converter.

- [#56](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/56) [`2436333`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/243633325c37e95fcaa9fafe18e668a4e5884d62) Thanks [@bielu](https://github.com/bielu)! - Added `Bielu.AspNetCore.AsyncApi.Overlay` and `Bielu.AspNetCore.Arazzo.Overlay`, which apply OpenAPI Overlays **inside the generation pipeline** rather than as a post-processing step: `options.AddOverlay("overlays/public.yaml")` on `AsyncApiOptions` or `ArazzoOptions` means `GET /asyncapi/v1.json` and `GET /arazzo/workflows.json` are already transformed — no build step and no second artifact to keep in sync. Overlays apply in registration order, work on JSON and YAML routes alike, and also run for build-time document generation so a checked-in document and the served one never disagree about whether the overlay ran. `ConfigureOverlays(o => o.Strict = true)` turns a `target` matching zero nodes from a logged warning into a failure, which is what you want in CI. Overlay files are read once on first use, so a missing file cannot break startup, and failures throw rather than silently serving an untransformed document.

  Overlays are applied at the **serialization boundary**, not through `AddDocumentTransformer` — that hands transformers a typed `AsyncApiDocument`, so an overlay there would need a serialize→overlay→deserialize round trip and would stake correctness on the serializer round-tripping losslessly. Overlay targets are JSONPath expressions over the wire representation, which has no faithful typed equivalent.

  Both packages are thin adapters over a new general-purpose hook in the packages they extend: `IAsyncApiSerializedDocumentTransformer` (with `AsyncApiOptions.AddSerializedDocumentTransformer`) in the core AsyncAPI package, and `IArazzoSerializedDocumentTransformer` in `Bielu.AspNetCore.Arazzo`. Both are usable on their own to rewrite a serialized document without taking any overlay dependency. As part of wiring this in, `AsyncApiDocumentProvider`'s AsyncAPI v3 build-time path now buffers through `AsyncApiSerializationHelper` instead of writing straight to the `TextWriter`; output is unchanged.

  Conformance: the OpenAPI Initiative's own fixtures are now vendored from the [Overlay-Specification](https://github.com/OAI/Overlay-Specification) repository (Apache-2.0) and run against the engine — 8 `compliant-sets` apply-semantics triples, which pass unmodified including under `Strict`, plus 67 document-validity fixtures across the 1.0 and 1.1 schemas. Those surfaced three real gaps in `OverlayValidator`, now fixed: duplicate `actions` entries are rejected (`uniqueItems`), unknown non-`x-` members are rejected on the root/`info`/action objects, and a malformed `overlay` version string such as `1.1` is now an **error** rather than a warning. A well-formed but unsupported version (`1.2.0`, `2.0.0`) remains a warning and still applies the newest known semantics, so forward compatibility is unchanged.

  Two upstream `pass/` fixtures are deliberately rejected, recorded with reasons in the conformance test: the specification's own traits example targets `$.paths.*.get[?@.x-oai-traits.paged]`, which is not valid RFC 9535 — member-name shorthand admits no hyphen, so the conformant spelling is `@['x-oai-traits']`. Overlay 1.1.0 pins `target` to RFC 9535 but upstream's schema only checks that it starts with `$`, which is why the fixture is filed as passing.

- [#55](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/55) [`5572daf`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/5572daf516f3e1a4535099eb7e216dc4d2ce08de) Thanks [@bielu](https://github.com/bielu)! - Added `Bielu.Overlay.NET` and `Bielu.Overlay.NET.Readers`, a framework-free object model, reader, validator, and apply engine for the [OpenAPI Overlay Specification](https://spec.openapis.org/overlay/latest.html) — the OAI's companion spec for declarative, repeatable transformations of an API description. Supports both 1.0.0 and 1.1.0 with version-gated semantics: 1.1.0 added the `copy` action, pinned `target` to RFC 9535 JSONPath, and legalized primitive targets and array concatenation, so a document declaring `1.0.0` gets 1.0.0 behaviour and using a 1.1.0-only feature there is reported rather than silently applied.

  The engine operates on `System.Text.Json.Nodes.JsonNode` and nothing else. The Overlay Specification is written against OpenAPI, but its mechanism — select nodes by JSONPath, then merge/copy/remove — carries no OpenAPI-specific assumptions, so **overlays here apply to AsyncAPI and Arazzo documents as readily as to OpenAPI ones**. That is not something an implementation bound to a particular object model can do, and it is the reason this exists in a repository that generates AsyncAPI.

  Note that targeting anything other than OpenAPI is **not sanctioned by the specification today**: the OAI closed [Overlay-Specification#268](https://github.com/OAI/Overlay-Specification/issues/268) as `not planned` in February 2026, on the grounds that it "is not a 'core function' of this specification, which is intended for OpenAPI descriptions". We have filed [#367](https://github.com/OAI/Overlay-Specification/issues/367) to revisit it, now with draft PR [#370](https://github.com/OAI/Overlay-Specification/pull/370) (`targetFormat`), arguing that released Arazzo 1.1.0 already normatively defines a source `type` of `"openapi" | "asyncapi" | "arazzo"`, so Overlay would be adopting an existing OAI value set rather than inventing a registry. That discussion is open, and an OAI maintainer has said the group remains "pretty well aligned" with the reasoning in #268, so treat AsyncAPI/Arazzo targeting as an extension this library offers rather than a conformance claim. Behaviour against OpenAPI documents is spec-exact regardless, keeping overlays portable to other tooling.

  Worth knowing when writing an Arazzo overlay: Arazzo keys `workflows`, `steps`, and `sourceDescriptions` as **arrays of objects carrying an id field**, where OpenAPI and AsyncAPI use maps. There is no `$.workflows.measureAndAlert` to target, so every Arazzo target is a filter expression (`$.workflows[?@.workflowId == 'measureAndAlert']`) and every removal deletes an array element rather than a map key. Both styles are covered by tests and shown side by side in the example. `OverlayApplier.Apply` never mutates its input, so one overlay can be applied to many documents; application is best-effort, reporting and skipping a failing action rather than aborting the rest; and an opt-in `Strict` mode turns a target matching zero nodes into an error, which is what a publishing pipeline wants.

  Also added `Bielu.Spec.Shared`, holding the YAML→`JsonNode` conversion previously private to `Bielu.Arazzo.NET.Readers`. Both spec libraries now share one copy of its plain-scalar type inference instead of duplicating it.

  Fixed in `Bielu.Arazzo.NET.Readers` at the same time: `ArazzoStringReader` decided between JSON and YAML by sniffing the first non-whitespace character, so a document written as a root-level YAML _flow mapping_ — `{ arazzo: 1.1.0, workflows: [...] }`, valid YAML that happens to begin with `{` — was handed to the JSON parser and rejected with a parse error rather than being read. JSON is now attempted first and YAML used as the fallback, so genuine JSON costs nothing while flow mappings parse correctly. `Bielu.Overlay.NET.Readers` never shipped with the bug.

  New `src/examples/OverlayDemo` applies an overlay to an AsyncAPI document end to end — removing an internal channel, filtering internal servers with an RFC 9535 filter function, merging into `info`, and replacing a primitive in place.

- [#48](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/48) [`7a28658`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/7a286589f44b73611b6ac30c5673e388696f0b54) Thanks [@bielu](https://github.com/bielu)! - Added Roslyn analyzers to provide compile-time diagnostics for common AsyncAPI attribute misuses (missing [AsyncApi], operations without channels, duplicate names, unused document names, invalid payload types, invalid JSON in examples, and missing parameterless constructors for example providers). The analyzers also verify ID naming conventions and ensure basic documentation is present. The analyzers are bundled into the Bielu.AspNetCore.AsyncApi.Attributes package.

- [#57](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/57) [`8123086`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/81230861140a284ed74c8a57bb0102d7cdea67f6) Thanks [@bielu](https://github.com/bielu)! - register the Aspire consoles through Scalar's `PluginUrls` instead of replacing Scalar's bundle. `WithSignalRClient(...)` / `WithGrpcClient(...)` now call `AddPluginUrl(...)` on `ScalarAspireOptions`, so the Scalar container keeps its own bundle and only the plugin is added — the two consoles can be enabled together, and neither pins the container to the Scalar version we built against. This needs `Scalar.AspNetCore` 2.16.17 / `Scalar.Aspire` 0.11.12, the first releases carrying `PluginUrls` (both pins bumped).

  `@bielu/scalar-signalr` and `@bielu/scalar-grpc` gained a `dist/scalar-plugin.mjs` build for it: an ES module whose **default export is the plugin**, which is the contract `pluginUrls` requires (Scalar `import()`s the URL and rejects a module whose default export is not a function). It is a distinct artifact from the existing outputs, which are not interchangeable with it — `dist/plugin.js` self-installs by hooking `window.Scalar`, `dist/plugin.mjs` (gRPC) is the library entry, and `dist/standalone.js` bundles Scalar itself. The in-process ASP.NET Core packages (`MapScalarSignalRAssets()` / `MapScalarGrpcAssets()`) are unchanged and still serve `dist/plugin.js`.

  Also fixed along the way: the `configure` callback on both Aspire extensions was **dead code**. It base64-encoded the configured AsyncAPI documents into the bundle URL's query string, but nothing ever read them back — the consoles silently fell back to discovering documents from the Scalar configuration. The new module reads the payload from its own `import.meta.url`, so `options.AddDocument(...)` now actually reaches the console. The gRPC console reads the same seam to locate its protobuf descriptor endpoint, since `document.currentScript` is always `null` inside an ES module.

  Breaking, for AppHost code that named the parameter or the constant: `ScalarSignalRAspireExtensions.DefaultBundleUrl` / `ScalarGrpcAspireExtensions.DefaultBundleUrl` are now `DefaultPluginUrl` (pointing at `dist/scalar-plugin.mjs`), and the optional `bundleUrl` parameter is now `pluginUrl`. Positional callers and the common `WithSignalRClient()` / `WithGrpcClient()` forms are unaffected. Renamed rather than deprecated because no stable release has shipped yet — and because a `public const` is inlined into consuming assemblies, so changing it after 1.0.0 would be a binary break.

- [#48](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/48) [`4345dd8`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/4345dd8d71a35660020a0aa1a48402ddf83a82a7) Thanks [@bielu](https://github.com/bielu)! - Add an interactive SignalR console for Scalar.
  Includes `Bielu.AspNetCore.AsyncApi.Scalar.SignalR` (ASP.NET Core) and `Bielu.AspNetCore.AsyncApi.Scalar.SignalR.Aspire` (Aspire hosting) packages.
  The console allows live interaction with SignalR hubs directly from the Scalar API Reference.

- [#48](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/48) [`14e85ae`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/14e85ae5d11a522ad25c7a665316f3c318432fb9) Thanks [@bielu](https://github.com/bielu)! - Added a new `dotnet new` template pack (`Bielu.AspNetCore.AsyncApi.Templates`) providing several templates to get started with AsyncAPI:

  - `asyncapi-webapi`: Minimal API with AsyncAPI and Scalar UI.
  - `asyncapi-signalr`: SignalR Hub with AsyncAPI and interactive console.
  - `asyncapi-grpc`: gRPC Service with AsyncAPI and interactive console.
  - `asyncapi-console`: Console Application (Worker Service) with AsyncAPI attributes.
  - `asyncapi-sln`: Multi-project solution (Contracts, API, Worker).

- [#48](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/48) [`b501e52`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/b501e52cf8dbc2cfbfcaa182b5858b98596fa4da) Thanks [@bielu](https://github.com/bielu)! - Add support for populating AsyncAPI descriptions from XML documentation comments.
  Includes new `IncludeXmlComments` extension methods on `AsyncApiOptions` and automatic fallback for channels, operations, messages, and schemas.

### Patch Changes

- [#60](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/60) [`17a4451`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/17a4451f3b4b277152abc5a9aade81c092089983) Thanks [@bielu](https://github.com/bielu)! - Ship the Native AOT runtime directives to consumers.

  The AOT work that made `README.md`'s "Native AOT support" claim true fixed the failure **in the
  example project**: `rd.xml` lived in `src/examples/AotStreetlights/` and was referenced by that one
  csproj. Every other application publishing Native AOT would have published, started, and then thrown
  on its first document request:

  ```text
  System.NotSupportedException: 'ByteBard.AsyncAPI.Models.ReferenceType[]' is missing native code or metadata.
  ```

  — with nothing in the documentation to explain why, because the cause lives in our dependency graph
  (`ByteBard.AsyncAPI` resolving enums from display names through `Array.CreateInstance`, which ILC
  cannot see) rather than in the consumer's code.

  The directives now ship inside the package as `buildTransitive/Bielu.AspNetCore.AsyncApi.rd.xml`,
  applied by a `.targets` alongside them whenever `PublishAot` is `true`. `buildTransitive` rather than
  `build`, because the core package is very often an indirect reference — through `.Merger`,
  `.Versioning`, a protocol extension or a Scalar console — and the directives are needed however it
  was acquired. Opt out with
  `<BieluAsyncApiIncludeRuntimeDirectives>false</BieluAsyncApiIncludeRuntimeDirectives>` to supply your
  own set.

  `scripts/verify-aot.sh` — and with it the blocking `aot-verification` CI job — now proves this from
  the consumer's side rather than ours. It packs the library into a local feed and AOT-publishes
  `src/examples/AotStreetlights` against it as an ordinary `PackageReference` consumer, outside the
  repository and inheriting none of its MSBuild conventions. The runtime directives reach that build
  only if they are packed to the right path and imported by NuGet: `RdXmlFile` resolves to
  `~/.nuget/packages/bielu.aspnetcore.asyncapi/<version>/buildTransitive/`, and the source generator
  arrives through the package's `analyzers/` folder the same way.

  The example keeps its project references by default, so `dotnet build` and the IDE work with no packing
  step; `-p:UseAsyncApiPackage=true` selects the package path. The script now compares **three**
  documents — AOT via project reference, AOT via package, and the reflection-based build — and requires
  all three to be equal once parsed as JSON. A wrong `PackagePath` fails the job instead of reaching
  consumers, which is what the previous arrangement could not catch.

  The [Native AOT](https://apidescriptions.bielu.pl/articles/native-aot.html) article documents what is
  supplied, why it is necessary, and how to override it.

- [#47](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/47) [`6ac6223`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/6ac62238f45adcdb78082940884a8beb2a4dff4b) Thanks [@bielu](https://github.com/bielu)! - fix: bump `vite` from `^5.4.0` (EOL, no security backports) to `^6.4.3` in the `@bielu/scalar-signalr` and `@bielu/scalar-grpc` asset packages, clearing all open Dependabot alerts: the `server.fs.deny` bypass on Windows alternate paths (GHSA-fx2h-pf6j-xcff, high), the optimized-deps `.map` path traversal (GHSA-4w7w-66w2-5vf9), the launch-editor NTLMv2 hash disclosure via UNC paths (GHSA-v6wh-96g9-6wx3) and — via the bundled esbuild 0.25 — the esbuild dev-server open CORS issue (GHSA-67mh-4wv8-2f99). All were development-scope (vite dev server only); shipped bundles were never affected. Bundles rebuilt and verified with vite 6.

- [#60](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/60) [`c82ff6a`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/c82ff6a5fe75697ce49d4c82ef3676f1eb729db6) Thanks [@bielu](https://github.com/bielu)! - Take the stable `ByteBard.AsyncAPI.NET` 3.0.1 instead of `3.0.1-beta.17`.

  This was the last thing standing between the packages and a defensible stable tag. Packing a stable
  1.0.0 while depending on a prerelease produced `NU5104` ("a stable release of a package should not
  have a prerelease dependency") — invisible for as long as our own version was `1.0.0-beta.*`, and a
  real problem once it is not: a stable release resting on a beta that can be unlisted or replaced.

  Same version number, prerelease suffix dropped, across all three packages (`ByteBard.AsyncAPI.NET`,
  `.Bindings`, `.Readers`). Verified by packing the full solution: **27 packages, zero NuGet warnings**,
  and the core package's nuspec now records `ByteBard.AsyncAPI.NET 3.0.1`. Full build and all 656 tests
  pass unchanged, which is the evidence that the stable release is the same code the betas were.

- [#60](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/60) [`5e5929f`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/5e5929f3fa7e6b2d7de0bb28190900cfc7224053) Thanks [@bielu](https://github.com/bielu)! - Clear the build warnings, and pin the package sources the build restores from.

  The solution built with 154 warnings. It now builds with 20, all of which are the trim/AOT warnings
  covered by the open annotation decision (`IL2026`, `IL3050`, `IL2072` on the reflection-based
  generation path, which is still the default).

  **A new `NuGet.config` is the largest single part of that.** There was none, so the build inherited
  whatever feeds the machine happened to have configured — 72 of the warnings were `NU1507` ("there are
  N package sources defined... please map your package sources"), one per project, on any machine with
  more than one feed. That is a reproducibility problem before it is a warning: every extra feed is
  another source that can answer for any package ID in `Directory.Packages.props`. The config clears
  inherited sources, declares nuget.org, and maps it explicitly, so adding a second feed later cannot
  silently start serving an existing package ID. `packageSourceMapping` clears inherited mappings too,
  because that section merges across config files on its own — clearing the sources does not stop a
  machine-level config from narrowing what nuget.org is allowed to answer for. A forced full restore
  confirms everything this repository consumes comes from nuget.org.

  Two of the fixes are behavioural rather than cosmetic:

  - **`AsyncApiOptions.DocumentRoutePattern` was never initialised** (`CS8618`), so it was null until
    `MapAsyncApi(pattern)` assigned it — a null anything reading it earlier would see, build-time
    generation included. It now defaults to `AsyncApiGeneratorConstants.DefaultAsyncApiRoute`.
  - **The merger swallowed cancellation.** `LoadContentAsync`'s catch-all (`CS0168`, on the discarded
    exception variable) reported _any_ failure as an unavailable source, including the
    `OperationCanceledException` from cancelling the merge itself. Cancellation now propagates; a
    per-source HTTP timeout still reports the source as unavailable, which is what the catch-all is for.
  - **The merger crashed when every source was unavailable.** Skipping unreadable sources left nothing
    to merge, and the merge itself then failed on `documents[0]` with an index exception that named
    neither the sources nor the cause — the CLI printed that as its error message. `MergeAsync` now
    throws `InvalidOperationException` listing the URIs that could not be loaded. Merging remains
    best-effort whenever at least one source succeeds.

  The rest are local: nullable annotations in tests and examples, three internal `async` methods gaining
  the `Async` suffix (`VSTHRD200`), an unread primary-constructor parameter, and an `IRouteConstraint`
  cref that cannot be written unambiguously because two shared-framework assemblies declare that type.

  `AsyncApiMessageExampleTests` also moves off `WebHostBuilder`/`TestServer(IWebHostBuilder)`
  (`ASPDEPR004`/`ASPDEPR008`, deprecations scheduled to become errors) and onto the
  `Host.CreateDefaultBuilder().ConfigureWebHostDefaults(... UseTestServer())` pattern the sibling
  integration tests already use.

- [#51](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/51) [`b4bd5d1`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/b4bd5d135c2ce308810ce1ace1334bcef91bca32) Thanks [@bielu](https://github.com/bielu)! - Migrated the documentation site from `asyncapi.bielu.pl` to `apidescriptions.bielu.pl` (`docs/CNAME`, `PackageProjectUrl` in `src/Directory.Build.props`, README links) ahead of the stable 1.0.0 tag, since package metadata is immutable once published. Split the docfx site into separate **AsyncAPI** and **Arazzo** sections — articles and API reference are now generated independently (`docs/api/asyncapi/`, `docs/api/arazzo/`, `docs/articles/arazzo/`) with their own top-level navigation — and replaced the landing page with a two-column overview introducing both specs.

  Also completed the repo-rename follow-through: the GitHub repo has actually been renamed to `bielu/Bielu.AspNetCore.ApiDescriptions` (confirmed via `gh repo view`), so `RepositoryUrl` in `src/Directory.Build.props`, the README CI badge, `globalMetadata.repository`/`docurl` in `docs/docfx.json`, `CONTRIBUTING.md`'s upstream remote, `PACKAGE.md`, and the Scalar gRPC/SignalR console asset READMEs now all point at the new repo.

- [#48](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/48) [`a2d1d93`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/a2d1d9312d0c47ce55c6c2aeec286e0c40eccc92) Thanks [@bielu](https://github.com/bielu)! - Created a new documentation site using docfx, hosted on GitHub Pages. The documentation includes detailed guides for getting started, configuration, attributes, and protocol-specific details for SignalR, gRPC, SSE, and WebRTC.

- [#58](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/58) [`7c4adc1`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/7c4adc1551ca4e423588d6213d7b1d5e06b8a032) Thanks [@bielu](https://github.com/bielu)! - complete the NuGet package metadata before the first stable tag, since it cannot be changed once a version is published.

  - **License.** No package carried any license metadata — a `LICENSE` file sat at the repo root but no `PackageLicenseExpression` was ever set, so every package would have published as "License: not specified". Now `MIT` (matching `LICENSE`) via `src/Directory.Build.props`, along with a `Copyright` field. Both are inherited by all 27 packages.
  - **Descriptions.** `Bielu.AspNetCore.AsyncApi` itself had none — NuGet substitutes the package id, so the flagship package would have shipped with "Bielu.AspNetCore.AsyncApi" as its entire description. Same for `.Attributes` and `.Templates`. `.Versioning`'s one-line description was expanded to match the detail level of its siblings.
  - **Tags.** 19 of 27 packages had no `PackageTags`, including the core package — which matters because staying discoverable on the `asyncapi` search term was the stated reason for keeping `AsyncApi` in the package ids in the first place.

  Also fixes a packaging bug found while auditing: `Bielu.AspNetCore.AsyncApi.SourceGenerators` was packable, so it produced a **standalone package with no lib and no content** alongside the copy correctly bundled into `Bielu.AspNetCore.AsyncApi`'s `analyzers/dotnet/cs` folder. It is now `IsPackable=false`, matching how `Bielu.AspNetCore.AsyncApi.Analyzers` is already handled inside the Attributes package. The bundled analyzer DLLs are unaffected — both were verified present in the packed output.

- [#58](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/58) [`6d06ac5`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/6d06ac5f805592edc8bc2751a763c01edec4627b) Thanks [@bielu](https://github.com/bielu)! - give every package its own nuget.org landing page. `src/Directory.Build.props` packed the **root README** into all 27 packages, so `Bielu.Overlay.NET`, `Bielu.Arazzo.NET`, `Bielu.Cli.Shared` and everything else displayed the `Bielu.AspNetCore.AsyncApi` pitch and quickstart — the wrong page for two thirds of the suite. Each package now ships a `PACKAGE.md` describing that package: what it is, how to install it, a usage example, and links to the relevant documentation article.

  A `PACKAGE.md` in the project directory is picked up automatically; projects without one still fall back to the root README, so adding a package does not break `dotnet pack` before its readme is written.

  The core package's `PACKAGE.md` already existed but was never referenced by any `PackageReadmeFile`, so it had never actually shipped. It is now wired up, and its stale reference to the removed `Bielu.AspNetCore.AsyncApi.UI` package is replaced with the current package list.

- [#40](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/40) [`7f1c1e4`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/7f1c1e4ea8c6e533fad77700ad531c45aafdf6e1) Thanks [@bielu](https://github.com/bielu)! - fix: pin `@asyncapi/react-component` to exactly 3.1.3 and override `@asyncapi/specs` to the safe 6.11.1 in Bielu.AspNetCore.AsyncApi.UI, so a lockfile-less `npm install` can never float to the malicious `@asyncapi/specs@6.11.2` published in the 14 July 2026 supply-chain attack (Miasma RAT). The committed lockfile already resolved only safe versions; none of the compromised `@asyncapi/generator*` packages are in the dependency graph.

- [#60](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/60) [`2e816ab`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/2e816abbe3d63b68be22a151784d0bdb746df6d1) Thanks [@bielu](https://github.com/bielu)! - Baseline the public API surface before the stable tag, and narrow three pieces of it that were public
  by accident.

  `Microsoft.CodeAnalysis.PublicApiAnalyzers` was enabled on `Bielu.AspNetCore.AsyncApi` but not
  maintained: `PublicAPI.Shipped.txt` was empty, `PublicAPI.Unshipped.txt` had 18 entries, and the
  analyzer reported **169 further symbols** as undeclared. The whole public surface of the flagship
  package was effectively untracked, which is exactly the gate that is supposed to catch an accidental
  break after 1.0.0. All 169 are now declared, and the analyzer is enabled on
  `Bielu.AspNetCore.AsyncApi.Attributes` too (81 entries) — that is the assembly consumers' own
  contract types compile against, so a break there breaks their build rather than their document.

  Reviewing the surface rather than just recording it found three things worth changing while changing
  them is still free:

  - **`AddServer`'s overloads no longer collide.** `AddServer(name, url, protocol, string? pathName = null)`
    had the same arity as `AddServer(name, url, protocol, Action<AsyncApiServer> configure)`. RS0027
    flags that as a backcompat hazard — an optional parameter must have the most parameters among its
    overloads — and it had a sharper consequence in practice: `AddServer(name, url, protocol, null)` did
    not compile at all, because `null` converts equally well to `string?` and to
    `Action<AsyncApiServer>` (CS0121).

    The optional parameter is gone and `configure` moved to a **fifth** parameter, so each overload now
    has an arity of its own and a bare `null` unambiguously means "no path". A regression test asserts
    it, by compiling that exact call.

    **This is a source break for the four-argument delegate form**: `AddServer(name, url, protocol, server => …)`
    becomes `AddServer(name, url, protocol, pathName: null, server => …)`. Every call site in the
    repository — templates, docs, examples and tests — is updated. It also fixes a real gap: the old
    `configure` overload hardcoded `PathName` to null, so a path and a configuration callback could not
    be used together. They can now.

  - **`AsyncApiOptions.ChannelBindings` / `OperationBindings` are now get-only.** Nothing in the repo
    assigned either dictionary; callers go through `AddChannelBinding`/`AddOperationBinding`. This is a
    deliberate pre-1.0 break with a real consequence: the dictionaries' _contents_ are still fully
    mutable, but a caller can no longer **replace the dictionary instance** the way
    `options.ChannelBindings = new(...)` allowed. Build the contents up instead. Taken now because a
    setter cannot be removed once the baseline freezes.
  - **`ParameterInfoExtensions` is now internal.** A reflection helper with one call site, published as
    an extension method on `System.Reflection.ParameterInfo`, where it surfaced in IntelliSense on every
    `ParameterInfo` in any file importing the namespace.

  The entries stay in `PublicAPI.Unshipped.txt` and move to `PublicAPI.Shipped.txt` when 1.0.0 tags,
  alongside the `BASYNC001`–`BASYNC009` analyzer rules moving to their `Release 1.0.0` section. That
  move is what turns RS0017 on: from then on, removing a shipped API is a build error rather than a
  discovery made by a consumer.

  The remaining 26 packable projects are deliberately not baselined yet — the spec libraries in
  particular are young enough that freezing them now would cost more than it protects.

- [#59](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/59) [`447d1b8`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/447d1b8cb91f493013ce77b384cc544e233b1dda) Thanks [@bielu](https://github.com/bielu)! - close the quality gates a stable release should hold.

  **A single-file bug.** `AsyncApiOptions.IncludeXmlComments(Assembly)` derived the XML path from `Assembly.Location`, which is an empty string for an assembly embedded in a single-file app — reducing the path to a bare `{Name}.xml` resolved against the current working directory, so XML descriptions silently went missing. It now falls back to `AppContext.BaseDirectory`.

  **The Native AOT example never actually published.** `PublishAot` flows into referenced projects as a global MSBuild property, so publishing `src/examples/AotStreetlights` failed with NETSDK1207 on the netstandard2.0 analyzer and source generator, which cannot be AOT-compiled. Both now opt out explicitly, and the new `scripts/verify-aot.sh` avoids passing `PublishAot` on the command line (a global property cannot be overridden by the projects it flows into). The example publishes through ILC now.

  **AOT verification in CI — and what it found.** Roadmap PR 9 scoped a step that publishes the AOT example and compares its document against the reflection-based build; it was never wired up. `scripts/verify-aot.sh` does exactly that, as a new `aot-verification` PR job.

  It found that **AsyncAPI generation did not work under Native AOT at all** — the document endpoint threw on every request. Three separate defects, all now fixed:

  1. `MapAsyncApi()` mapped its handler as a parameter-bound lambda, so `RequestDelegateFactory` tried to resolve `JsonTypeInfo` for the handler's own return type and threw. Now mapped as a `RequestDelegate` — which also clears the two IL warnings that were sitting on that call site.
  2. `AddAsyncApiGeneratedMetadata(...)` registered the generated provider under the **caller's** casing while `AddAsyncApi` registers keyed services **lowercased**. The `Replace` matched nothing, so the reflection provider stayed live and threw `PlatformNotSupportedException`. The source generator's entire reason for existing silently did not take effect for any document name containing an uppercase character — it worked by accident only for an all-lowercase name.
  3. `ByteBard.AsyncAPI` resolves enums from display names through `Enum.GetValues()`, which calls `Array.CreateInstance(typeof(TEnum), n)`. Constructing an array type at runtime needs metadata ILC does not emit unless told the type is used dynamically, so the endpoint threw `'ByteBard.AsyncAPI.Models.ReferenceType[]' is missing native code or metadata`. A scoped `rd.xml` roots the assembly **and each enum's array type** — the array types must be named individually, because an array is a constructed type ILC only emits when it can see it used, and `Array.CreateInstance` is invisible to that analysis. Neither `TrimmerRootAssembly` nor rooting the assembly alone is sufficient; both were tried and the failure survived unchanged.

  **Native AOT now works**, and the `aot-verification` job is **blocking**: it publishes the example both ways and asserts the served documents are identical, which is what guards against the source generator and the reflection scan silently diverging.

  `README.md`'s "✅ Native AOT support" is now a claim the build actually enforces.

  **Analyzer release tracking.** The Analyzers project had no `AnalyzerReleases.{Shipped,Unshipped}.md`, producing 9 × RS2008 and leaving the `BASYNC001`–`BASYNC009` rules undeclared — which is how consumers find out a rule was added or changed between versions. All nine are now recorded as unshipped; they move to a `Release 1.0.0` section when the stable version tags.

  **XML documentation.** Fixed every broken `<see cref>` in the core package (CS1574) plus the surrounding CS1572/CS1573. Several were wrong rather than merely unresolvable: `AsyncApiOptions.AsyncApiVersion` documented a default of `AsyncApiSpecVersion.AsyncApi3_1` — a type that does not exist, naming a value that is not the default — and `IAsyncApiOperationTransformer`'s `<param>` tags described the wrong parameters. These feed the published API reference.

  **Trim annotations.** Annotated the reflection-based helpers (`HasBindAsyncMethod`, `HasTryParseMethod`, the authorization scanner) so their trim behaviour is declared rather than inferred, and replaced a `MakeGenericType` call — which native AOT cannot do — with an equivalent structural check. That also removed a dead comparison against an open generic type, which a return type can never equal.

  Solution warnings drop from 278 to 247; all 656 tests still pass.

- [#48](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/48) [`1315fc8`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/1315fc8302ca16bad8c42cfe307fea2e8a6c564f) Thanks [@bielu](https://github.com/bielu)! - Remove the obsolete `Bielu.AspNetCore.AsyncApi.UI` package.
  Users should use `Scalar.AspNetCore` or another interactive documentation tool instead.
  The `MapAsyncApiUi()` extension method and all UI-related assets have been removed from the solution.

## Pre-release channel history

Reconstructed from the packages published to the `beta` channel on NuGet.org. Dates are the first
`1.0.0-beta.*` publish for each package (all packages share the `1.0.0` base version).

### 2026-06-21 — protocol bindings

- **SignalR protocol bindings** — first `beta` publish of
  `Bielu.AspNetCore.AsyncApi.Extensions.Protocols.SignalR`, a custom `signalr` protocol with channel,
  operation, message and server bindings (plus the runnable `SignalRChat` example).
- **gRPC protocol bindings** — first `beta` publish of
  `Bielu.AspNetCore.AsyncApi.Extensions.Protocols.Grpc`, a custom `grpc` protocol with channel,
  operation, message and server bindings.

### 2026-03-12 — document merging

- **Merger** — first `beta` publish of `Bielu.AspNetCore.AsyncApi.Merger`, which merges multiple
  AsyncAPI documents into one.

### 2026-03-11 — CLI & build-time document generation

- **CLI** — first `beta` publish of `Bielu.AspNetCore.AsyncApi.Cli` (`get-document` / `merge`
  commands).
- **Build-time document generation** — first `beta` publish of
  `Bielu.AspNetCore.AsyncApi.ApiDescription.Server`, emitting the AsyncAPI document at build time.

### 2026-02-02 — initial beta

- First `beta` publish of the core packages: `Bielu.AspNetCore.AsyncApi`,
  `Bielu.AspNetCore.AsyncApi.Attributes` and `Bielu.AspNetCore.AsyncApi.UI`. This is the initial
  Saunter rewrite: fluent `AddAsyncApi()` configuration, document/schema transformers, separated
  core/attributes/UI packages, ByteBard.AsyncAPI.NET for schema handling and .NET 10 targeting
  (see [Changes from Saunter](#changes-from-saunter)).

## Changes from Saunter

This section documents the key differences between Bielu.AspNetCore.AsyncApi and the original [Saunter](https://github.com/asyncapi/saunter) library.

### New Features

- **Fluent Configuration API** - New `AddAsyncApi()` method with fluent builder pattern inspired by Microsoft.AspNetCore.OpenApi
  ```csharp
  // New fluent API
  builder.Services.AddAsyncApi(options =>
  {
      options.AddServer("mosquitto", "test.mosquitto.org", "mqtt");
      options.WithDescription("My API");
      options.WithLicense("MIT", "https://opensource.org/licenses/MIT");
  });
  ```

- **Document Transformers** - Support for `IDocumentTransformer` to customize the generated AsyncAPI document
- **Schema Transformers** - Support for `ISchemaTransformer` to customize generated schemas
- **Separate UI Package** - `Bielu.AspNetCore.AsyncApi.UI` as a standalone package with modern AsyncAPI React components
- **Separate Attributes Package** - `Bielu.AspNetCore.AsyncApi.Attributes` for annotation-only scenarios
- **.NET 10 Support** - Updated to target .NET 10

### Breaking Changes from Saunter

#### Namespace Changes

| Saunter (Old) | Bielu.AspNetCore.AsyncApi (New) |
|---------------|----------------------------------|
| `Saunter.AsyncApiSchema.v2` | `ByteBard.AsyncAPI.Models` |
| `Saunter.Attributes` | `Bielu.AspNetCore.AsyncApi.Attributes.Attributes` |
| `Saunter` | `Bielu.AspNetCore.AsyncApi.Extensions` |

#### API Changes

| Saunter (Old) | Bielu.AspNetCore.AsyncApi (New) |
|---------------|----------------------------------|
| `AddAsyncApiSchemaGeneration()` | `AddAsyncApi()` |
| `MapAsyncApiDocuments()` | `MapAsyncApi()` |
| `options.AssemblyMarkerTypes` | Auto-discovery via attributes |
| `options.AsyncApi = new AsyncApiDocument {...}` | Fluent builder: `options.AddServer()`, `options.WithDescription()` |

#### Data Structure Changes

- All data structure names now have an `AsyncApi` prefix:
  - `Info` → `AsyncApiInfo`
  - `Server` → `AsyncApiServer`
  - `License` → `AsyncApiLicense`
  - `Contact` → `AsyncApiContact`
- All data structure constructors are now parameterless

#### Dependency Changes

| Saunter | Bielu.AspNetCore.AsyncApi |
|---------|---------------------------|
| LEGO AsyncAPI.NET | ByteBard.AsyncAPI.NET |
| AsyncAPI.NET.Bindings | AsyncAPI.NET.Bindings (same) |

### Migration Example

**Before (Saunter):**
```csharp
services.AddAsyncApiSchemaGeneration(options =>
{
    options.AssemblyMarkerTypes = new[] { typeof(MyMessageBus) };
    options.AsyncApi = new AsyncApiDocument
    {
        Info = new Info("My API", "1.0.0"),
        Servers = 
        {
            ["mqtt"] = new Server("broker.example.com", "mqtt")
        }
    };
});

app.UseEndpoints(endpoints =>
{
    endpoints.MapAsyncApiDocuments();
    endpoints.MapAsyncApiUi();
});
```

**After (Bielu.AspNetCore.AsyncApi):**
```csharp
builder.Services.AddAsyncApi(options =>
{
    options.AddServer("mqtt", "broker.example.com", "mqtt");
    options.WithTitle("My API")
        .WithVersion("1.0.0");
});

app.MapAsyncApi();
app.MapAsyncApiUi();
```

### Package Comparison

| Feature | Saunter | Bielu.AspNetCore.AsyncApi |
|---------|---------|---------------------------|
| NuGet Package | `Saunter` | `Bielu.AspNetCore.AsyncApi` |
| Attributes Package | Included | `Bielu.AspNetCore.AsyncApi.Attributes` |
| UI Package | Included | `Bielu.AspNetCore.AsyncApi.UI` |
| Target Framework | .NET 6+ | .NET 10 |
| AsyncAPI Version | 2.x | 2.6.0 |
| Configuration Style | Object initialization | Fluent API |
| Document Transformers | Filters | Transformers |

## Version History

### v1.0.0 (Upcoming)

Initial release of Bielu.AspNetCore.AsyncApi with the following features:

- Complete rewrite of configuration API with fluent builder pattern
- Separated packages for core, attributes, and UI
- Document and schema transformers
- Updated to ByteBard.AsyncAPI.NET for schema handling
- .NET 10 support
- Improved endpoint routing with `MapAsyncApi()` and `MapAsyncApiUi()`

---

## Attribution

This project is based on [Saunter](https://github.com/asyncapi/saunter) by the AsyncAPI Initiative and draws inspiration from [Microsoft.AspNetCore.OpenApi](https://github.com/dotnet/aspnetcore) for its API design.