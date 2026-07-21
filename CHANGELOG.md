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