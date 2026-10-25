# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and from now on the
`[Unreleased]` section and released version entries are managed with
[changesets](https://github.com/changesets/changesets) — see [`.changeset/README.md`](.changeset/README.md).

This project is a fork/evolution of [Saunter](https://github.com/asyncapi/saunter), the original AsyncAPI documentation generator for .NET. Below you'll find both the version history for Bielu.AspNetCore.AsyncApi and a comparison of changes from the original Saunter library.

> **Versioning note.** Every NuGet package in this repository shares a single version (in
> `version.props`), so they are always released together. The first stable release, `1.0.0`, shipped
> on 2026-08-02; before it, the suite was published only to the **`beta` pre-release channel** on
> NuGet.org (`1.0.0-beta.*`). The [Pre-release channel history](#pre-release-channel-history) below
> reconstructs, from the published packages, when each package and capability first became available
> on that channel.

## [Unreleased]

_Nothing yet._

## [2.0.0] - 2026-10-25

### Major Changes

- [#131](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/131) [`135457f`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/135457f7a6f34563e3ea96ab8681627d33a51280) Thanks [@bielu](https://github.com/bielu)! - The attribute types moved from `Bielu.AspNetCore.AsyncApi.Attributes.Attributes` to `Bielu.AspNetCore.AsyncApi.Attributes`, dropping the doubled `Attributes` segment. Replace `using Bielu.AspNetCore.AsyncApi.Attributes.Attributes;` with `using Bielu.AspNetCore.AsyncApi.Attributes;`. The types and their members are unchanged.

- [#130](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/130) [`dfc3304`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/dfc330424f77c7557a9c70357981901ed784c0e7) Thanks [@bielu](https://github.com/bielu)! - Update the ByteBard.AsyncAPI.NET dependencies (`ByteBard.AsyncAPI.NET`, `.Bindings`, `.Readers`) from 3.0.1 to 4.0.0-beta.20. ByteBard 4.0 contains breaking changes, and its object model is exposed through this package's public API, so code that uses ByteBard types directly may need updating.

- [#134](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/134) [`13b3f6c`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/13b3f6c351551d4c5f5fbb7e0017575dde04f811) Thanks [@bielu](https://github.com/bielu)! - Public collections are now typed as interfaces, so their implementation can change later without another breaking release:

  - `AsyncApiOptions.OperationBindings`, `ChannelBindings`, `MessageBindings` and `ServerBindings` are `IDictionary<string, IList<…>>` (were `Dictionary<…>`).
  - `AsyncApiOptions.IncludeOnlyChannels` is `ISet<string>` (was `HashSet<string>`). It still compares case-insensitively.
  - `AsyncApiTypeMetadata.Members` and `AsyncApiMemberMetadata.Parameters`, `Messages`, `Operations` and `MessageExamples` are `IReadOnlyList<…>` (were `List<…>`). A custom `IAsyncApiMetadataProvider` can keep passing `List<…>` instances; code that added to these lists after construction must build them up front instead.

  Code that only reads these collections, or adds through the `Add…` methods, keeps compiling unless it names the concrete type, for example `Dictionary<…> bindings = options.ChannelBindings;`.

- [#133](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/133) [`03fd585`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/03fd58554f764d73d839c36872aa2f6e63213783) Thanks [@bielu](https://github.com/bielu)! - `AsyncApiGeneratorConstants.DefaultAsyncApiVersion` is renamed to `DefaultInfoVersion`. Its value (`"1.0.0"`) is the default `info.version`, the version of the described API, and the old name read as the AsyncAPI specification version, which is `AsyncApiOptions.AsyncApiVersion`. The public constants on `AsyncApiGeneratorConstants` now have XML docs.

- [#168](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/168) [`f4fc674`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/f4fc674b21fc571e4643be4775570e1a109c3595) Thanks [@bielu](https://github.com/bielu)! - `AddAsyncApi()` and `AddAsyncApiForApiVersions()` no longer call `AddEndpointsApiExplorer()`. Document generation hasn't used ApiExplorer since the transformer contexts stopped exposing it, so registering it only added services the app might not want.

  An app that relied on `AddAsyncApi()` to register the endpoint ApiExplorer for something else, such as Swashbuckle's `AddSwaggerGen()` describing minimal APIs, must now call `builder.Services.AddEndpointsApiExplorer()` itself. That includes apps that also use controllers: `AddControllers()` registers the ApiExplorer for controllers only, so without the call their minimal APIs drop out of the Swagger document. `AddOpenApi()` registers what it needs, so apps using it are unaffected. So are apps using `AddAsyncApiForApiVersions()`, since Asp.Versioning's `AddApiExplorer()`, which it requires, registers the endpoint ApiExplorer as well.

- [#136](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/136) [`a60cb61`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/a60cb61c87546d38f40734a20d15ccb7d201ed48) Thanks [@bielu](https://github.com/bielu)! - `AsyncApiOptions.ShouldInclude` is removed. It was a `Func<ApiDescription, bool>` carried over from `Microsoft.AspNetCore.OpenApi`, but document generation never called it, so setting it had no effect. Channels come from attributes and integrations, not from ApiExplorer descriptions. Use `AddIncludedChannel(...)` / `IncludeOnlyChannels` to limit a document to specific channels, or a document transformer for anything more selective.

  `Bielu.AspNetCore.AsyncApi.Versioning` set `ShouldInclude` to filter each version's document by its ApiExplorer group. Since nothing read it, versioned documents were never filtered, and that doesn't change: each still gets the version's `info` and deprecation flag.

- [#132](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/132) [`072dd74`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/072dd74ccad4df17122dd8602b7ca4933d26b336) Thanks [@bielu](https://github.com/bielu)! - Operation attributes are now named from the application's point of view, as in AsyncAPI 3: `[SendOperation]` (`action: send`) and `[ReceiveOperation]` (`action: receive`). `OperationType` is now `Send`/`Receive` instead of `Publish`/`Subscribe`.

  `[PublishOperation]` and `[SubscribeOperation]` still work, produce the same operations as before, and are marked `[Obsolete]`. They used the AsyncAPI 2.x names, which describe the operation from the client's side, so the mapping is inverted: replace `[PublishOperation]` with **`[ReceiveOperation]`** and `[SubscribeOperation]` with **`[SendOperation]`**. A plain find-and-replace of Publish→Send would reverse your operations.

  Operations without an explicit `OperationId` get a default ID ending in the operation type, so those IDs change from `{Type}_{Method}_Publish`/`_Subscribe` to `{Type}_{Method}_Receive`/`_Send`. Set `OperationId` explicitly if something (an Arazzo workflow, a client generator) depends on the old ID.

  The source generator now also emits custom attributes derived from `[SendOperation]` or `[ReceiveOperation]`, which it used to drop silently, so the generated document matches the reflection-based one. Such an attribute must be `internal` or `public`; otherwise the new **BASYNC011** warning reports that its operation is left out of the generated document.

- [#135](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/135) [`c9a6589`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/c9a6589e7a617844a96133fd3dd28cc96805585a) Thanks [@bielu](https://github.com/bielu)! - The transformer contexts no longer expose MVC ApiExplorer types. These members came from `Microsoft.AspNetCore.OpenApi`, which builds operations from HTTP endpoints. AsyncAPI documents here are built from attributes and integrations, so the members were empty or never set:

  - `AsyncApiOperationTransformerContext.Description` (`ApiDescription?`) is removed. It was always `null`.
  - `AsyncApiJsonSchemaTransformerContext.ParameterDescription` (`ApiParameterDescription?`) is removed. It was always `null` unless a transformer passed one to `GetOrCreateSchemaAsync` itself.
  - The `parameterDescription` parameter is removed from `GetOrCreateSchemaAsync` on all three contexts. It is now `GetOrCreateSchemaAsync(Type type, CancellationToken cancellationToken = default)`, so a call passing `cancellationToken:` by name keeps compiling.
  - `AsyncApiDocumentTransformerContext.DescriptionGroups` is removed. It listed the app's HTTP ApiExplorer groups. Resolve `IApiDescriptionGroupCollectionProvider` from `context.ApplicationServices` if a document transformer needs them.

### Minor Changes

- [#156](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/156) [`cb627ee`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/cb627ee76724afc92ad191147609731d9b9b7163) Thanks [@bielu](https://github.com/bielu)! - Add `WithApiCatalogItem()` to `Bielu.AspNetCore.ApiCatalog`: an endpoint convention that lists an API served from an endpoint rather than a document, such as an MCP server, as an `item` link in `/.well-known/api-catalog`. `app.MapMcp("/mcp").WithApiCatalogItem("MCP server")` lists the MCP endpoint once, however many methods it maps; routes with parameters are left out. The SignalR chat example now serves its MCP server and the API catalog together.

- [#144](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/144) [`5237114`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/5237114c03df1c2fe10c637a4eaa106929d54fda) Thanks [@bielu](https://github.com/bielu)! - Add `Bielu.AspNetCore.ApiCatalog`, a new package that serves an RFC 9727 API catalog. `MapApiCatalog()` answers `GET` and `HEAD /.well-known/api-catalog` with an RFC 9264 Linkset (`application/linkset+json` with the RFC 9727 profile) that lists every document the app serves as a `service-desc` link: those of `MapAsyncApi()`, `MapArazzo()` and `MapOpenApi()`, with their media types. `AddApiCatalog()` adds `service-doc` links (`AddServiceDoc`), other APIs or nested catalogs (`AddItem`), and the route of a non-default `MapOpenApi()` (`OpenApiRoutePattern`).

  `Bielu.AspNetCore.ApiDescriptions` gains `ApiDescriptionDocumentMetadata`, which `MapAsyncApi()` and `MapArazzo()` now add to their endpoints so the catalog can find them. An endpoint of your own can carry it too.

- [#149](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/149) [`2c0cee5`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/2c0cee5f904c96b9ed0c7723db78c67031ead399) Thanks [@bielu](https://github.com/bielu)! - Add `Bielu.AspNetCore.AsyncApi.Avro`, a new package that describes message payloads as Apache Avro schemas. `AsyncApiOptions.AddAvroPayloads()` replaces each message's JSON Schema payload with the equivalent Avro schema, declared as `schemaFormat: application/vnd.apache.avro;version=1.9.0`. Objects become records, nullable and optional properties become unions with `null`, and arrays, maps, string enums, `int32`/`int64` and `float`/`double` are kept. The `uuid`, `date-time`, `date` and `time` formats become logical types, and recursive types are referenced by name. `WithNamespace` sets the namespace of named types, and `ForMessages` limits which messages are converted. The JSON Schemas stay under `components/schemas`, and headers are unchanged.

- [#146](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/146) [`598f71b`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/598f71b261e04068a53dd70ded394994732e58d6) Thanks [@bielu](https://github.com/bielu)! - `Bielu.AspNetCore.AsyncApi.Wolverine` now describes the CloudEvents Wolverine sends. `WolverineAsyncApiOptions.DescribeCloudEvents()` turns the messages of every endpoint that sends CloudEvents into the structured-mode envelope that Wolverine's `CloudEventsMapper` writes: `type` pinned to the message type name, `source` to `WolverineOptions.ServiceName`, `datacontenttype` to `application/json; charset=utf-8`, the payload as `data`, and Wolverine's `tenantid`, `topic`, `traceid` and `traceparent` extensions. Those endpoints are the SignalR transport, broker endpoints configured with `InteropWithCloudEvents()` (detected), and any endpoint passed to `DescribeCloudEvents(...)`. Other endpoints keep the plain message; a message routed both ways gets a `{message}.cloudevent` copy for its CloudEvents channels. The package now depends on `Bielu.AspNetCore.AsyncApi.CloudEvents`, and `AddCloudEvents()` skips messages that are already structured CloudEvents.

- [#145](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/145) [`1e7af6b`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/1e7af6b50959ebbd292db8ab52f47c2ca7587cf2) Thanks [@bielu](https://github.com/bielu)! - Add `Bielu.AspNetCore.AsyncApi.CloudEvents`, a new package that describes a document's messages as CloudEvents 1.0 events. `AsyncApiOptions.AddCloudEvents()` adds the required `id`, `source`, `specversion` and `type` attributes and the optional `dataschema`, `subject` and `time` to each message's headers (binary mode, the default). The header names follow the message's protocol binding, chosen from the protocol of its channels' servers: `ce-` for HTTP, `ce_` for Kafka, `cloudEvents:` for AMQP, and no prefix for MQTT 5. Existing headers are combined with `allOf`. `UseMode(CloudEventsMode.Structured)` instead wraps the payload in the `application/cloudevents+json` envelope. The event type (`WithTypePrefix`, defaulting to the message name) and source (`WithSource`) are pinned with `const` and can be overridden per message (`ConfigureMessage`). Message examples gain the attributes too.

- [#156](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/156) [`1290d2e`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/1290d2edaaa00df6ca828e89e593785346d5000c) Thanks [@bielu](https://github.com/bielu)! - Add `Bielu.AspNetCore.ApiDescriptions.Mcp`, a new experimental package (diagnostic `BIELUMCP001`) that lets AI agents build integrations from an app's own API descriptions through a Model Context Protocol server. `IMcpServerBuilder.WithApiDescriptions()` exposes every AsyncAPI, OpenAPI and Arazzo document the app serves as the resource template `apidescription://{kind}/{name}`, plus six read-only tools. `list_api_descriptions` lists the documents with their titles, specification versions and the tools that can inspect each. `find_api_operations` searches AsyncAPI operations across documents and returns a handle for each match. `get_api_operation` returns one operation's complete documented contract: channel, servers, security, messages and schemas, bindings, reply and examples, with references keyed by canonical JSON Pointer and what the document does not specify listed. These three return `structuredContent` with an `outputSchema`. `get_api_description`, `list_asyncapi_operations` and `get_asyncapi_message` return a whole document, an operation summary and one message with its references. Messages are identified by canonical pointer, so aliases and inline messages resolve. The documents are written as build-time generation writes them, overlays included. The package is built on the official `ModelContextProtocol` C# SDK 2.3, and the SignalR chat example serves it at `/mcp`.

- [#150](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/150) [`6d4c93c`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/6d4c93cfcb8ab2e9bebd78eabc6ef3beeadc16ea) Thanks [@bielu](https://github.com/bielu)! - Add `Bielu.AspNetCore.AsyncApi.Protobuf`, a new package that describes payloads whose types are Protocol Buffers messages by their `.proto` source. `AsyncApiOptions.AddProtobufPayloads()` replaces the payload of every message whose type is a generated `IMessage` with `schemaFormat: application/vnd.google.protobuf;version=3` (or `2` for a proto2 file). The source is a string rendered from the message descriptor, so field numbers, scalar types, labels, proto2 defaults, maps, oneofs, nested types, enums and reserved ranges are the real wire contract. It is one self-contained file: the payload message is marked `@RootNode` and declared with every message and enum it uses, and the `google/protobuf` and `google/type` types stay imports. Other payloads keep their JSON Schema, as do messages from editions files and messages that use types from a file of another syntax, which one file can't describe faithfully. `ForMessages` limits which messages are converted.

- [#152](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/152) [`cd88871`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/cd88871efd24753b02a44c674970b0b667708cc1) Thanks [@bielu](https://github.com/bielu)! - Add `Bielu.AspNetCore.XRegistry`, a new package that serves the messages and payload schemas of an app's AsyncAPI documents as a read-only CNCF xRegistry (1.0-rc4). `MapXRegistry()` exposes each AsyncAPI document as a message group and a schema group. Every message is a message definition that points at its payload schema with `dataschemaformat`, `dataschemauri` and `dataschemaxid`. Schemas are served as standalone JSON Schema (draft-07), Avro or Protobuf documents with `xRegistry-` headers, and `$details` returns their metadata. The registry answers the standard read API: the registry, `/capabilities`, `/model`, groups, resources, `meta` and Versions, with the `?inline` flag and RFC 9457 errors. It also serves host-based discovery at `/.well-known/xregistry`. `AddXRegistry()` sets the registry id and name and limits which documents are included.

### Patch Changes

- [#167](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/167) [`718d0dc`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/718d0dc3abe202f1410d046bf9ad9ea89593725d) Thanks [@bielu](https://github.com/bielu)! - Update `ByteBard.AsyncAPI.NET` and `ByteBard.AsyncAPI.NET.Readers` to 4.0.0-beta.21, which includes ByteBardOrg/AsyncAPI.NET#56, #58, #59 and #60. AsyncAPI 2.x documents with message traits serialize again instead of returning 500. `ByteBard.AsyncAPI.NET.Bindings` stays at 4.0.0-beta.20, the latest version published.

- [#171](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/171) [`b9ebc32`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/b9ebc3236b7043d8d19977bd1cf039f2aea0b259) Thanks [@bielu](https://github.com/bielu)! - Update `ByteBard.AsyncAPI.NET.Bindings` to 4.0.0-beta.21, so all three ByteBard packages are on the same version.

- [#137](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/137) [`13ad45f`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/13ad45f4f713956557b94c030e283b1be4188904) Thanks [@bielu](https://github.com/bielu)! - Drop the two AsyncAPI 2.x writer workarounds, now fixed in ByteBard (ByteBardOrg/AsyncAPI.NET#57, #58). Channel servers are referenced as `#/servers/{name}` for both versions and the 2.x writer outputs the server name. A message trait without headers is no longer given an empty `headers` schema in 2.x documents. Requires the ByteBard release that contains those fixes and ByteBardOrg/AsyncAPI.NET#56 (the JsonPointer.Net 7 conflict).

- [#137](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/137) [`5f47549`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/5f47549546f172f30bf7925fec596530eb08520b) Thanks [@bielu](https://github.com/bielu)! - The broker-bindings docs no longer list the three ByteBard 3.0.1 binding bugs (WebSockets key, Kafka `schemaIdLocation` casing, SQS `fifoQueue`) as known issues, since ByteBard 4.0 fixes them. Tests now validate all three against the official AsyncAPI schema.

- [#121](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/121) [`b5e3085`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/b5e3085235cae9992314a8e31c32ac2c6b9265b2) Thanks [@bielu](https://github.com/bielu)! - Internal cleanup: remove an unreachable private helper from `AsyncApiEndpointConventionBuilderExtensions`. No public API or behavior change. The repository's test-coverage measurement now covers only library code.

- [#148](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/148) [`bc25730`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/bc25730b719ead82764f10422b80df58b0504aad) Thanks [@bielu](https://github.com/bielu)! - Packages now carry their family's icon on nuget.org instead of all sharing the AsyncAPI logo: protocol bindings, Scalar consoles, Arazzo, Overlay, and CLI & build tooling each have their own, drawn in the same style. The AsyncAPI core packages keep the existing logo.

- [#122](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/122) [`4837cb2`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/4837cb20d61c009e62dfd0caacc6c0cbc3f8dff8) Thanks [@bielu](https://github.com/bielu)! - Fix document generation failing for any message payload with a JSON Patch property (`JsonPatchDocument` or `JsonPatchDocument<T>`). The RFC 6902 operation schemas now close their objects with the equivalent `"additionalProperties": { "not": {} }` instead of the boolean `false`, which could not be read back into the schema model. Unused internal code left over from the `Microsoft.AspNetCore.OpenApi` port is also removed.

- [#153](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/153) [`e6a25a6`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/e6a25a664abf14a6970af6babc4ecee892041469) Thanks [@bielu](https://github.com/bielu)! - `Bielu.AspNetCore.AsyncApi.Scalar.SignalR.Aspire` and `Bielu.AspNetCore.AsyncApi.Scalar.Grpc.Aspire` now depend on MessagePack 2.5.307 directly. Before, they picked up 2.5.301 through `Scalar.Aspire` → `Aspire.Hosting` → `StreamJsonRpc`, which is affected by [GHSA-qhrr-8q5h-9q3h](https://github.com/advisories/GHSA-qhrr-8q5h-9q3h).

- [#127](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/127) [`4462c91`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/4462c9181044f27fa5ee1044e80e5bb15a3fd46a) Thanks [@bielu](https://github.com/bielu)! - The NuGet packages now pass NuGet Package Explorer's health checks (Source Link, deterministic, compiler flags). Each package ships a `.snupkg` symbol package to the nuget.org symbol server, with Source Link pointing at the GitHub commit, and CI builds set `ContinuousIntegrationBuild` so the build is deterministic.

- [#155](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/155) [`82d7c65`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/82d7c65d11e64c3374ff5f309074901ea16c2d64) Thanks [@bielu](https://github.com/bielu)! - The standalone SignalR and gRPC Scalar console bundles now include `@scalar/api-reference` 1.76.0 (was 1.62.9). The newer Scalar no longer pulls in the `ai` / `@ai-sdk/*` versions affected by [GHSA-866g-f22w-33x8](https://github.com/advisories/GHSA-866g-f22w-33x8) or the zod affected by [GHSA-mrhx-c7xc-m3w5](https://github.com/advisories/GHSA-mrhx-c7xc-m3w5). The console build tooling also moves to vite 6.4.5, which fixes the vite and launch-editor advisories flagged by Dependabot.

## [1.4.0] - 2026-10-17

### Minor Changes

- [#110](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/110) [`8832287`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/8832287ff8c678d146826fa85eda3689eed9b64b) Thanks [@bielu](https://github.com/bielu)! - Generate Arazzo documents at build time. `dotnet getdocument` and `dotnet asyncapi getdocument` now also write the app's `AddArazzo` documents, with the version from `ArazzoOptions.Version` and serialized-document transformers (such as overlays) applied. An Arazzo document is listed under its own name, or under an `arazzo-` prefix when an AsyncAPI or OpenAPI document already has that name (`MyApi_arazzo-v1.json`); set `ArazzoOptions.BuildTimeDocumentName` to choose another name. `AddArazzo` works with or without `AddAsyncApi`, in any order.

- [#107](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/107) [`dd6b4cd`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/dd6b4cdb0d8fe2ab1e7654ac1563e3ee38136cf4) Thanks [@bielu](https://github.com/bielu)! - Add `Bielu.AspNetCore.ApiDescriptions`, a new package that owns build-time document generation (`dotnet getdocument`) for every document family. Packages register an `IBuildTimeDocumentSource` with `AddBuildTimeDocumentSource<TSource>()`, and one provider lists and writes their documents next to `Microsoft.AspNetCore.OpenApi`'s. OpenAPI names are settled first, then sources in ascending `Order`; a document keeps its own name unless an earlier document already uses it, in which case it gets its source's prefix. `Bielu.AspNetCore.AsyncApi` now depends on it and registers AsyncAPI as the first source, so existing AsyncAPI and OpenAPI file names are unchanged. Asking build-time generation for a document name it doesn't list now throws instead of being treated as an AsyncAPI document name.

- [#111](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/111) [`66ac36e`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/66ac36ea85e54a2af195397d5c07694a9796c14d) Thanks [@bielu](https://github.com/bielu)! - The source generator now picks up `[Message]` placed on a payload class, so class-level message metadata applies under Native AOT and the source-generated document matches the reflection-based one. `IAsyncApiMetadataProvider` gains a default interface member, `GetPayloadMessageAttributes()`, that the generated provider implements; providers that leave it at `null` keep reading the attribute by reflection.

- [#109](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/109) [`7c4ca87`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/7c4ca87179209c2b4694048cd629ca59e5e3bbe7) Thanks [@bielu](https://github.com/bielu)! - `Bielu.AspNetCore.Arazzo` now ships its own Roslyn analyzers, with `BARAZZO` rule ids, which check workflow-builder code at compile time:

  - **BARAZZO001** (warning): `AddOpenApi()` is called after `AddArazzo()` in the same method, so build-time generation may miss the Arazzo documents.
  - **BARAZZO002** (error): a literal workflow id, step id or source name, or a marker type's derived id, isn't a valid Arazzo identifier. The builders throw this at startup.
  - **BARAZZO003** (error): a step sets no target, or more than one, of `Operation`/`OperationPath`/`Channel`/`Workflow`.
  - **BARAZZO004** (error): a `Channel` step's action is neither `"send"` nor `"receive"`.
  - **BARAZZO005** (warning): `WithCorrelationId` is set on a step that doesn't receive.
  - **BARAZZO006** (warning): a step names a source description the document never declares.
  - **BARAZZO007** (warning): `DependsOn` or `Workflow` names a step or workflow the document never defines. Nothing checked this before, not even at runtime.
  - **BARAZZO008** (info): `AddAsyncApiSource`/`AddOpenApiSource` points at a document the project never registers.

  Existing code that sets an invalid id, two step targets or an unknown action already fails at startup, and now fails to compile instead. The build-time generation docs also state exactly which registration orders affect which tools.

- [#62](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/62) [`b205404`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/b2054049584860d1c50a97637e3dfc83dbdf02fe) Thanks [@bielu](https://github.com/bielu)! - Added `Bielu.AspNetCore.AsyncApi.Scalar.Broker` and the `@bielu/scalar-broker` npm bundle: an
  interactive message-broker console for the Scalar API Reference, mirroring the SignalR and gRPC
  consoles on the shared `Bielu.AspNetCore.AsyncApi.Scalar` / `@bielu/scalar-core` foundation. The
  console reads `kafka`, `mqtt` and `amqp` bindings out of your AsyncAPI document(s), prefills a
  publish editor from the channel's message payload schema, and tails the channel live.

  A browser cannot speak any of these protocols, so — unlike the gRPC console, which reaches the
  server directly over gRPC-Web — this console goes through a server-side bridge that
  `MapScalarBrokerAssets()` mounts alongside the bundle: `GET {path}/connections`,
  `POST {path}/publish`, and `GET {path}/tail` (Server-Sent Events). Driver packages implement
  `IBrokerBridge` and register connections through `AddScalarBrokerBridge(o => o.Add…Connection(…))`;
  bridges are built lazily, so a broker that is unreachable at startup cannot stop the app from
  starting.

  **The bridge can publish to your broker, so it is not open by default.** Nothing is exposed until
  `MapScalarBrokerAssets()` is called, and that call returns a convention builder covering all three
  proxy endpoints so one `RequireAuthorization(...)` protects them together. Without authorization
  metadata the proxy allows requests only in the Development environment (logging a warning once) and
  answers `403` anywhere else, unless the operator explicitly sets `AllowAnonymous`. The bundle itself
  stays unauthenticated: it is static JavaScript holding no configuration or secrets, and it is inert
  without the proxy behind it.

  Two consequences of that shape worth recording. Tail subscriptions are ephemeral and start at the
  newest offset, so opening a console never disturbs a real consumer group's committed position and
  never replays a backlog into the browser. And the tail stream is read with `fetch` +
  `ReadableStream` rather than `EventSource`, because `EventSource` cannot send the `Authorization`
  header the proxy sits behind — which also lets Scalar's auth panel drive the proxy, including API
  keys in a query parameter, which the gRPC console has to downgrade to a header.

  `Bielu.AspNetCore.AsyncApi.Scalar.Broker.Kafka` is the first driver, and the reference for the next
  ones. Tailing a topic uses a throwaway consumer group (`bielu-scalar-broker-{guid}`) starting at
  `AutoOffsetReset.Latest` with auto-commit off, so opening a console cannot move a real consumer
  group's committed offsets or replay a backlog into someone's browser. `Confluent.Kafka` is pinned to
  `2.14.0`, the version `Aspire.Confluent.Kafka` 13.4.6 already depends on, so the Aspire mini-shop
  resolves one copy rather than two. The connection descriptor sent to the browser carries a redacted
  endpoint only — any `user:password@` in `bootstrapServers` is stripped, and a plain `host:port` is
  left alone.

  Wired into the Aspire mini-shop's Order Service, which already publishes order events to Kafka and
  already declares `kafka` channel bindings.

  This package ships no `dist/scalar-plugin.mjs` and no standalone bundle, and there is no `.Aspire`
  companion. All three exist to load a console into the Scalar _container_ from a CDN, and a
  CDN-hosted broker plugin would have no server-side bridge to talk to — the broker console has to be
  mapped in the app that owns the broker connection.

- [#108](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/108) [`5d7c967`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/5d7c9679cc932f66a337be76652d2f2c3f040fd3) Thanks [@bielu](https://github.com/bielu)! - The Wolverine integration now reads `[Message]` placed on message classes (title, summary, description, name, content type, headers type, bindings, correlation id, external docs, traits and tags). Explicit `WolverineAsyncApiOptions` calls such as `WithExternalDocs<T>` take precedence over the attribute, `IncludeCorrelationId()` does not override an attribute-declared correlation id, and the component key stays Wolverine's message type name (`Name` only changes the displayed `name`; `MessageId` is ignored). The attribute-to-message mapping is now shared with the core attribute pipeline.

### Patch Changes

- [#120](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/120) [`ce37962`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/ce37962c2a516d93ac10c5944f0b92cadc90b587) Thanks [@bielu](https://github.com/bielu)! - The broker console no longer offers a connection whose protocol doesn't match the selected channel. It used to fall back to every registered connection, so with only the Kafka driver installed, an `mqtt` or `amqp` channel was offered the Kafka connection, and publishing would write to a Kafka topic named after the MQTT topic or AMQP queue. Such a channel now shows that the server has no connection for its protocol. The README, docs and package readmes now say Kafka is the only driver that ships today.

- [#116](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/116) [`02a4697`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/02a469792f9505b8cda06c50044444d8a82e8082) Thanks [@bielu](https://github.com/bielu)! - Every package now ships its `LICENSE` file, and the files it produces carry the MIT license and the copyright notice: an `AssemblyMetadata("License", "SPDX-License-Identifier: MIT")` attribute in each assembly, a comment in each XML documentation file, a header in the packed MSBuild and `rd.xml` files, and a copyright line in each `PACKAGE.md`. License scanners such as ClearlyDefined can now find the license in the package files as well as in the nuspec.

## [1.3.0] - 2026-10-16

### Minor Changes

- [#100](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/100) [`71df387`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/71df387c4ef22080c38bc376e6a81bd6f97f952c) Thanks [@bielu](https://github.com/bielu)! - Add `ArazzoOptions.Version` to choose the Arazzo Specification version of the served document (`ArazzoVersion.V1_0` or `V1_1`). It defaults to `V1_1`, the version emitted before.

- [#106](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/106) [`5ff3d07`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/5ff3d07ec30adb83def2caf99670a7fea75acf0a) Thanks [@bielu](https://github.com/bielu)! - Fix build-time generation (`dotnet getdocument`, `OpenApiGenerateDocuments=true`) in apps that call both `AddOpenApi()` and `AddAsyncApi()`. Previously only the AsyncAPI document was written, to `<Project>.json`, and no OpenAPI document was written. Both are now written. An AsyncAPI document whose name matches an OpenAPI one (both default to `v1`) is written as `<Project>_asyncapi-<name>.json`. Set the new `AsyncApiOptions.BuildTimeDocumentName` to choose a different name.

  Add the **BASYNC010** analyzer warning, reported when `AddOpenApi()` is called after `AddAsyncApi()` in the same method. Register OpenAPI first so build-time generation reliably writes both documents.

  The BASYNC analyzers now reach apps that reference only `Bielu.AspNetCore.AsyncApi`. Previously they ran only when `Bielu.AspNetCore.AsyncApi.Attributes` was referenced directly, so existing BASYNC warnings may appear for the first time after upgrading.

- [#105](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/105) [`d5b9fa1`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/d5b9fa18dbc601a70da0607aa7ce3d3de156acce) Thanks [@bielu](https://github.com/bielu)! - Allow `[Message]` on a payload class. The class is the payload, so the type argument is optional (`new MessageAttribute()`), and its values become defaults wherever the class is used as a payload; a `[Message]` on a method still overrides them. `MessageAttribute.PayloadType` is now nullable. Class-level metadata is read by reflection and is not yet part of the source-generated (Native AOT) metadata.

- [#104](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/104) [`b841c13`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/b841c13acc87e5b1c9509066495bc74dba1d5242) Thanks [@bielu](https://github.com/bielu)! - Document more of the AsyncAPI spec from Wolverine routing: `AddWolverine` gains `IncludeCorrelationId()`, `WithExternalDocs<T>()`, `WithMessageTraits<T>()`, `WithOperationTraits<T>()` and `RequireSecurity<T>()`.

### Patch Changes

- [#101](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/101) [`26fc9ff`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/26fc9ff2a9ce7eb2a4bc83bf6c8f84f935b6238f) Thanks [@bielu](https://github.com/bielu)! - Fix the source generator dropping `MessageAttribute.ContentType`, so source-generated (Native AOT) metadata now sets the same message content type as the reflection path.

## [1.2.0] - 2026-10-11

### Minor Changes

- [#90](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/90) [`5b092b2`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/5b092b2457be39208a30267351d5ba736c77c7cf) Thanks [@bielu](https://github.com/bielu)! - Add `ExternalDocsUrl` and `ExternalDocsDescription` to `[Channel]`, `[PublishOperation]`/`[SubscribeOperation]` and `[Message]`. Setting a URL writes `externalDocs` on the generated channel, operation or message, including through the source generator.

- [#97](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/97) [`05b3a50`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/05b3a50e56951593a754c261434d46f1774ec4ce) Thanks [@bielu](https://github.com/bielu)! - Add `AsyncApiDocument.IsAsyncApiVersion(AsyncApiVersion)`, a public extension that tells whether a document declares a given AsyncAPI major version, so transformers no longer need to inspect the `asyncapi` string themselves.

- [#88](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/88) [`19d6ec3`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/19d6ec369dc7a63059ca798883c7b40c5852fd41) Thanks [@bielu](https://github.com/bielu)! - Add `MessageAttribute.CorrelationIdLocation` and `CorrelationIdDescription`. Setting a location (for example `$message.header#/correlationId`) writes a `correlationId` on the generated message, including through the source generator.

- [#86](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/86) [`a5c08bd`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/a5c08bd28370362b976d1adb87eeb91339992528) Thanks [@bielu](https://github.com/bielu)! - Add `AsyncApiOptions.AddMessageBinding` and `AsyncApiOptions.AddServerBinding`. Message bindings are registered in `components/messageBindings` and attached to messages declared with a matching `[Message(BindingsRef = "...")]`, which was previously ignored. Server bindings are registered in `components/serverBindings` and attached to the server of the same name.

- [#93](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/93) [`8e320d4`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/8e320d466b48174d3e655f519639a3fb720f64eb) Thanks [@bielu](https://github.com/bielu)! - Add explicit request/reply declaration to operation attributes: `ReplyChannel`, `ReplyAddressLocation`, `ReplyAddressDescription` and `ReplyMessageIds` on `[PublishOperation]`/`[SubscribeOperation]` write the operation's `reply` object (AsyncAPI 3.x).

- [#91](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/91) [`aef1d49`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/aef1d498b248b6193e39211ada543827449501aa) Thanks [@bielu](https://github.com/bielu)! - Add attribute-declared security requirements. `SecuritySchemes` on `[PublishOperation]`/`[SubscribeOperation]` and the new `AsyncApiOptions.AddServerSecurity(...)` reference schemes registered with the new `AsyncApiOptions.AddSecurityScheme(...)`, writing `security` on the generated operation or server.

- [#92](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/92) [`193f99c`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/193f99c382d76dcdda6c80f222f09fe2a0bcac64) Thanks [@bielu](https://github.com/bielu)! - Add operation and message traits. Register them with `AsyncApiOptions.AddOperationTrait(...)`/`AddMessageTrait(...)` and apply them with `Traits` on `[PublishOperation]`/`[SubscribeOperation]` and `[Message]`, which writes `traits` references on the generated operation or message.

- [#97](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/97) [`50a65d2`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/50a65d275a5303c36354469d83b59141f9f6c45c) Thanks [@bielu](https://github.com/bielu)! - Document Wolverine handler responses as operation replies. A `receive` operation whose handler returns a message that has no route now gets a `reply`, addressed by the `reply-uri` header, on an address-less reply channel (AsyncAPI 3.x). `IncludeReplies()` extends this to returned messages that also have a route.

### Patch Changes

- [#87](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/87) [`e36cf8a`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/e36cf8a7201d3299554d72721011813ca569b747) Thanks [@bielu](https://github.com/bielu)! - Fix `AsyncApiOptions.AddChannelBinding` not attaching to the channel of the same name. The binding was registered in `components/channelBindings` but only reached a channel that named it through `[Channel(BindingsRef = "...")]`. A channel without a `BindingsRef` now gets the bindings registered under its own name; an explicit `BindingsRef` still takes precedence.

- [#83](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/83) [`60123ed`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/60123ed6ed8b5b297d2f1c824769a6a048351af3) Thanks [@bielu](https://github.com/bielu)! - Fix `MessageAttribute.Tags` being silently ignored. Tags passed to `[Message(typeof(T), "tag", ...)]` are now written to the generated message's `tags` and registered in `components/tags`, the same way operation tags already are.

## [1.1.0] - 2026-10-06

### Minor Changes

- [#77](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/77) [`88cada7`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/88cada7269e25842b97c8811e95986200a16809a) Thanks [@kboga](https://github.com/kboga)! - `[Message]` now honors `HeadersType`: the headers type gets a schema under `components/schemas` and is referenced from the message's `headers`. Added `MessageAttribute.ContentType` to set a message's `contentType` (falls back to the document's `defaultContentType` when unset).

- [#81](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/81) [`4429b97`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/4429b97ed4b09712e7f467fa566eb6459e31fe0d) Thanks [@kboga](https://github.com/kboga)! - Message example providers can also implement the new `IAsyncApiMessageHeaderExampleProvider` to supply a headers example (an object mapping header names to values) alongside the payload example.

- [#80](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/80) [`7f4a42c`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/7f4a42c4b05aa5d61723a15dce5b90c317215574) Thanks [@kboga](https://github.com/kboga)! - Added `AsyncApiOptions.AsyncApiJsonSchemaJsonOptions` (previously documented but missing): a per-document `JsonSerializerOptions` used only for schema generation, e.g. to document payload properties in PascalCase without changing the app's runtime JSON options.

- [#82](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/82) [`1300af4`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/1300af406e3be3942d8207095e9ce6df247b9188) Thanks [@bielu](https://github.com/bielu)! - New `Bielu.AspNetCore.AsyncApi.Wolverine` package. `options.AddWolverine(...)` builds channels, operations and messages from Wolverine's message routing: each route to an external transport (Kafka, SignalR, ...) becomes a `send` operation, each listener with a default incoming message type a `receive` operation, and messages are named by Wolverine's message type name (`[MessageIdentity]`), so the document uses the same names as the wire. SignalR channels resolve to the path the `WolverineHub` is mapped at and carry `signalr` bindings targeting `ReceiveMessage`; Kafka channels carry a `kafka` topic binding. Payload schemas are keyed like the attribute pipeline's, honoring `AsyncApiOptions.CreateSchemaReferenceId` (and inlined when it returns `null`); two message types whose schema ids collide fail generation with a pointer to `CreateSchemaReferenceId` rather than sharing one schema. Every route gets its own channel and operation, even when several endpoints resolve to the same address.

### Patch Changes

- [#74](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/74) [`f365e26`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/f365e26f48f20a10c9e0123720288343a44e100b) Thanks [@bielu](https://github.com/bielu)! - Document the .NET support matrix and Microsoft-aligned EOL policy, including planned .NET 11 and .NET 12 support
  and removal of EOL targets in major releases. This documentation change does not add new framework targets.

- [#79](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/79) [`fd80dcd`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/fd80dcdf2c31238bf3e196e438c479033f46383c) Thanks [@kboga](https://github.com/kboga)! - Nullable payload properties keep `null` in their schema type instead of collapsing to the non-null type (e.g. `string?` is documented as `["null", "string"]`), including nullable nested object properties.

- [#70](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/70) [`9057d87`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/9057d87eee68740d8db35354ee8d552ad383d5ed) Thanks [@bielu](https://github.com/bielu)! - Registered operation transformers (`AddOperationTransformer`, delegate/instance/type-based) are now actually invoked while generating operations from `[AsyncApi]` attribute metadata. Previously they were activated and disposed but never executed. `AsyncApiOperationTransformerContext.Description` is now nullable, since operations discovered from attribute metadata have no corresponding `ApiDescription`.

- [#76](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/76) [`45eb108`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/45eb10850a439afefaf164229e4575ddb8e6d3e1) Thanks [@kboga](https://github.com/kboga)! - `float`, `double` and `decimal` payload properties are now always documented as `type: number` with their format, even when System.Text.Json reports a string wire type (e.g. `[JsonNumberHandling(JsonNumberHandling.WriteAsString)]`). The leftover numeric-string `pattern` is dropped along with the `string` type. Nullable variants keep `null`.

- [#72](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/72) [`e934e07`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/e934e07343b5f6983b7f577d149a2bf4d7badf4c) Thanks [@bielu](https://github.com/bielu)! - Fix `AsyncApiDocumentService` silently dropping payload data whenever two different types (or two different operations/messages) produced the same sanitized component key. Schema and message component keys were derived independently from the bare CLR type name (`payloadType.Name`), completely ignoring a configured `AsyncApiOptions.CreateSchemaReferenceId`, and whenever the computed key already existed in `components.schemas`/`components.messages` the existing entry was silently kept — so two payload classes with the same short name in different namespaces (e.g. `Order.V1.Created` and `Order.V2.Created`), two different closed generic types over the same open generic definition (e.g. `Envelope<Foo>` and `Envelope<Bar>`, whose CLR `Type.Name` is identical regardless of the type argument), or two different operations/messages that happened to sanitize to the same id, would generate successfully but with one of the two payloads' schema/properties missing from the document entirely, with no warning.

  Schema and message component keys are now derived through the same authoritative path the schema generator already uses to resolve `CreateSchemaReferenceId` for a type, so a custom reference id is honored consistently everywhere a component key is built, and returning `null` from it reliably inlines the schema (per its documented contract) instead of still being added to `components/schemas` under a default key. When two genuinely different payload types, messages, or operations would still collide on the same key after sanitization, generation now throws a clear `InvalidOperationException` naming both conflicting types/members instead of silently discarding one of them — the same payload `Type` reused across multiple channels continues to be deduplicated into a single shared component, which is unaffected by this change.

- [#71](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/71) [`68106c5`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/68106c5ce932b60e8627a4eadd535dc96d5764f7) Thanks [@bielu](https://github.com/bielu)! - Fix `AsyncApiDocumentService` leaking disposable schema/operation transformer instances whenever document generation failed. Activation (`InitializeTransformers`, which turns a type-based `AddSchemaTransformer<T>`/`AddOperationTransformer<T>` registration into a per-request instance) and attribute-based population both ran outside the `try/finally` that disposed transformers, so an exception thrown while activating a transformer or while generating schemas/messages/operations (including a schema transformer itself throwing, or a cancelled `CancellationToken`) skipped disposal entirely for every instance already created — a straightforward resource leak for any transformer that owns unmanaged resources. The finalizer also disposed every instance in one pass without a fallback for a disposal failure part-way through, so one throwing `Dispose()`/`DisposeAsync()` stopped the remaining instances from being released, and a disposal failure occurring alongside a generation failure could hide the original error.

  Generation now owns exactly the transformer instances it activates per request: each type-based transformer is tracked the moment it is successfully constructed, so a partial activation failure (e.g. the third of four registered transformers throws in its constructor) still disposes the ones already created. Cleanup always runs — on success, on a population/generation failure, and on cancellation — and always attempts every owned instance even if disposing an earlier one throws; a disposal failure is now itself surfaced (as an `AggregateException` alongside the original error when both occurred, or directly when generation otherwise succeeded) instead of being silently swallowed or masking the real failure. A caller-supplied transformer instance (registered via `AddSchemaTransformer(IAsyncApiSchemaTransformer)`/`AddOperationTransformer(IAsyncApiOperationTransformer)`, which is reused across every generation request) is never disposed by this per-request cleanup — that instance's lifetime remains the caller's responsibility, same as before.

## [1.0.2] - 2026-08-23

### Patch Changes

- [#68](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/pull/68) [`573cc66`](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/commit/573cc66c48b6f1215c078f6c128b05bec9c1c73d) Thanks [@bielu](https://github.com/bielu)! - Fix `AsyncApiJsonSchemaService` throwing when a documented message type has a property that produces the JSON Schema `"enum"` keyword — most commonly an enum serialized with `JsonStringEnumConverter`. `System.Text.Json.Schema.JsonSchemaExporter` emits that keyword as a plain array of values, but the generated schema's `Enum` property is typed `IList<AsyncApiAny>`, and no `JsonConverter` was registered for `AsyncApiAny` anywhere in the schema-deserialization pipeline used by `AsyncApiJsonSchemaService`, so `JsonSerializer.Deserialize` threw `JsonException` the moment that keyword appeared — for any consumer whose documented AsyncAPI message contained an enum property. Adds `AsyncApiAnyJsonConverter` and registers it on the `JsonSerializerOptions` used to build `AsyncApiJsonSchemaContext`.

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

### Additional Changes

These shipped in `1.0.0` but predate the changeset workflow, so they have no generated entry above.

- **Server-Sent Events (SSE) protocol bindings** - New `Bielu.AspNetCore.AsyncApi.Extensions.Protocols.Sse`
  package providing a custom `sse` protocol with channel, operation, message and server bindings
  modelling the `text/event-stream` (`event`/`id`/`retry`/`data`) wire shape.
- **WebRTC protocol bindings** - New `Bielu.AspNetCore.AsyncApi.Extensions.Protocols.WebRtc` package
  providing a custom `webrtc` protocol with channel, operation, message and server bindings covering
  `RTCDataChannel` streams and SDP/ICE signaling.
- **Changeset-driven release workflow** - Contributor changes are now recorded as
  [changesets](https://github.com/changesets/changesets); the shared NuGet version and this changelog
  are updated from them via `scripts/apply-nuget-version.mjs`.
- **Fixed:** `BindingsRef` on `[Channel]` and operation attributes now actually attaches the referenced
  binding (registered via `AddChannelBinding`/`AddOperationBinding`) to the channel/operation in the
  generated document. Previously the binding was only stored under `components` and never linked.

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

### v1.0.0 — 2026-08-02

Initial stable release of Bielu.AspNetCore.AsyncApi with the following features:

- Complete rewrite of configuration API with fluent builder pattern
- Separated packages for core, attributes, protocol bindings and the Scalar consoles
- Document and schema transformers
- Updated to ByteBard.AsyncAPI.NET for schema handling
- .NET 10 support
- Improved endpoint routing with `MapAsyncApi()`

---

## Attribution

This project is based on [Saunter](https://github.com/asyncapi/saunter) by the AsyncAPI Initiative and draws inspiration from [Microsoft.AspNetCore.OpenApi](https://github.com/dotnet/aspnetcore) for its API design.