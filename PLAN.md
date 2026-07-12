# Plan: interactive gRPC console for Scalar (mirroring SignalR)

Goal: add a live gRPC console to the Scalar API Reference, the same way the SignalR console works —
driven by the `grpc` AsyncAPI bindings that already exist
(`Bielu.AspNetCore.AsyncApi.Extensions.Protocols.Grpc`).

## Layers (same shape as SignalR)

1. **`Bielu.AspNetCore.AsyncApi.Scalar.Grpc`** (NuGet) — serves an embedded JS bundle via
   `MapScalarGrpcAssets()` and injects it into Scalar via `WithGrpcClient()`.
2. **`@bielu/scalar-grpc`** (npm, in `assets/`) — a Vue web component registered as a Scalar plugin
   that parses `grpc` bindings and drives live calls.
3. **`Bielu.AspNetCore.AsyncApi.Scalar.Grpc.Aspire`** — swaps the Scalar container's bundle URL for
   a CDN-hosted `@bielu/scalar-grpc`.

## The one hard problem: browsers can't speak native gRPC

- Native gRPC needs HTTP/2 trailers browsers don't expose → the console must use **gRPC-Web**. Target
  apps must add `Grpc.AspNetCore.Web` (`UseGrpcWeb()` + `EnableGrpcWeb()` / `DefaultEnabled = true`).
- AsyncAPI payload schemas are JSON Schema — no protobuf field numbers, so they render request forms
  but **cannot** encode wire messages. The console needs real protobuf descriptors.
- Standard gRPC reflection is bidi-streaming, which gRPC-Web can't call. So the .NET package serves
  descriptors itself: `GET {assetsPath}/descriptors` returning a serialized `FileDescriptorSet`,
  gathered from the DI-registered gRPC services (each generated service exposes its `FileDescriptor`).
- Client: `@bufbuild/protobuf` (registry from the descriptor set) + `@connectrpc/connect-web`
  gRPC-Web transport → fully dynamic calls, no browser codegen. Auth headers from Scalar's auth state
  work unchanged (gRPC-Web metadata is plain HTTP headers).
- **Accept this limitation:** gRPC-Web only supports **unary + server-streaming**. Client-/bidi-
  streaming methods (already in `GrpcMethodType`) render as documentation-only with a "not invokable
  from the browser" badge.

## Status

- [x] **Phase 1 — shared foundation.** `Bielu.AspNetCore.AsyncApi.Scalar` (NuGet) + private
  `@bielu/scalar-core` (npm) extracted; SignalR packages refactored onto them. Full solution builds,
  SignalR bundle builds end-to-end. See `memory/scalar-console-shared-package.md` for the reuse contract.

- [ ] **Phase 2 — `Bielu.AspNetCore.AsyncApi.Scalar.Grpc` + `@bielu/scalar-grpc`.**
  - .NET: `ScalarGrpcDefaults` (`/bielu/scalar/grpc`), `ScalarGrpcOptions : ScalarPluginDocumentOptions<>`,
    `WithGrpcClient()` (→ `WithAsyncApiPluginScript`, global `__BIELU_SCALAR_GRPC__`),
    `MapScalarGrpcAssets()` (→ `MapScalarPluginBundle`), import `ScalarPluginBundle.targets`.
  - **New (no SignalR analog):** the `{assetsPath}/descriptors` endpoint returning the
    `FileDescriptorSet` from registered gRPC services (`application/x-protobuf`).
  - npm: thin `index.ts`/`plugin.ts`/`discovery.ts`/`auth.ts` over `@bielu/scalar-core` (element
    `bielu-grpc-console`, plugin `bielu-grpc`, sidebar "gRPC", `wrappedFlag __bieluGrpcWrapped`).
    Declare public types locally — do NOT re-export core types.
  - `grpc-bindings.ts` — parse `grpc` server/channel/operation bindings into service models
    (service, package, protoFile, method, methodType, request/response types, deadline, idempotency).
  - `GrpcConsole.vue` — methods grouped by service; JSON request editor prefilled from the payload
    schema (`exampleFromSchema`); invoke unary + server-streaming via connect-web over gRPC-Web using
    the fetched descriptor registry; metadata editor + Scalar auth passthrough; response/stream log.

- [ ] **Phase 3 — example, Aspire, wiring.**
  - `GrpcGreeter`: add `UseGrpcWeb(DefaultEnabled = true)`, `MapScalarGrpcAssets()`, `WithGrpcClient()`
    (it already has unary + server-streaming methods). Optionally mirror `SecureChatHub` for the auth path.
  - `Bielu.AspNetCore.AsyncApi.Scalar.Grpc.Aspire` (CDN bundle-URL swap; keep own options, do not take
    the AspNetCore framework ref — same call as `Scalar.SignalR.Aspire`). Needs CORS for gRPC-Web
    response headers (`grpc-status`, `grpc-message` exposed) since the Scalar container calls the origin.
  - Solution `.slnx`, `README.md`, `CHANGELOG.md`, `AGENTS.md` package table.
  - **CI:** the templates' `ciCombined.yml`/`release.yml` take a single npm `working-directory` (pointed
    at SignalR assets). Publishing a second npm package needs a list/matrix input in
    `bielu/bielu.GithubActions.Templates` (checked out in this workspace) or a second job pair.
  - **Tests:** endpoint tests (bundle + descriptor endpoint served; 404 body when bundle missing) and a
    `FileDescriptorSet` round-trip test — the descriptor endpoint is genuinely new logic, not boilerplate.

## Suggested order
Phase 1 ✓ → gRPC skeleton + static console (render from bindings, no invoke) → descriptor endpoint +
unary invoke → server-streaming + auth → Aspire + CI/docs.
