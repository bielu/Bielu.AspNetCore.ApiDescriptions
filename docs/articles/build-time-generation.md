# Build-Time Document Generation

You can generate AsyncAPI documents at build time, which is useful for CI pipelines, source control tracking, and distributing API specs without launching the app.

## Option 1: Using Microsoft.Extensions.ApiDescription.Server (Recommended)

The `Bielu.AspNetCore.AsyncApi` library is compatible with `Microsoft.Extensions.ApiDescription.Server`, which uses the built-in `dotnet getdocument` tool:

```bash
dotnet add package Microsoft.Extensions.ApiDescription.Server
```

Then configure your `.csproj`:

```xml
<PropertyGroup>
    <OpenApiGenerateDocuments>true</OpenApiGenerateDocuments>
    <OpenApiDocumentsDirectory>$(MSBuildProjectDirectory)</OpenApiDocumentsDirectory>
    <OpenApiGenerateDocumentsOnBuild>true</OpenApiGenerateDocumentsOnBuild>
</PropertyGroup>
```

AsyncAPI documents will be generated during `dotnet build` using the `IDocumentProvider` interface registered by `AddAsyncApi()`. That provider lives in the `Bielu.AspNetCore.ApiDescriptions` package (a dependency of `Bielu.AspNetCore.AsyncApi`), which lets other document families, such as Arazzo, be written by the same run through an `IBuildTimeDocumentSource`.

### Alongside `Microsoft.AspNetCore.OpenApi`

An app can call both `AddOpenApi()` and `AddAsyncApi()`, and build-time generation then writes both the OpenAPI and the AsyncAPI documents. Call `AddOpenApi()` first:

```csharp
builder.Services.AddOpenApi();
builder.Services.AddAsyncApi();
```

`AddAsyncApi()` makes build-time generation serve both libraries' documents by redirecting the document provider that `AddOpenApi()` has already registered. An `AddOpenApi()` call made afterwards registers that provider again, so depending on which provider `dotnet getdocument` picks, the AsyncAPI documents can be missing. The **BASYNC010** analyzer warns when `AddOpenApi()` follows `AddAsyncApi()` or `AddAsyncApiForApiVersions()` in the same method, and **BARAZZO001** warns when it follows `AddArazzo()`. It can't compare calls made from different methods, so keep these registrations in one place, such as `Program.cs`.

#### Ordering

What depends on registration order, and what doesn't:

| Situation | `AddOpenApi()` first | `AddOpenApi()` after `AddAsyncApi()` |
|-----------|----------------------|--------------------------------------|
| `dotnet asyncapi getdocument` (this repo's CLI) | Both documents | Both documents |
| Resolving this library's `IDocumentProvider` | Both documents | Both documents |
| `dotnet getdocument` / `Microsoft.Extensions.ApiDescription.Server`, when it finds OpenAPI's `IDocumentProvider` type | Both documents (the proxy forwards to ours) | **OpenAPI documents only** |
| `dotnet getdocument`, when it finds this library's type | Both documents | Both documents |

`AddOpenApi()` registers its provider with a plain `AddSingleton`, and the container resolves the last registration of a service type, so a later `AddOpenApi()` call replaces the proxy that `AddAsyncApi()` installed. `AddAsyncApi()` can't prevent that: an `IServiceCollection` extension only sees the registrations made so far, and neither the .NET container nor OpenAPI's provider offers a hook that runs after the caller's later registrations and before the container is built. The Microsoft tool picks the first `IDocumentProvider` type among the loaded assemblies, so which row applies depends on assembly load order. The CLI shipped here prefers this library's type and is therefore order-independent. Keep `AddOpenApi()` first when you use the Microsoft tool.

`dotnet getdocument` writes the `v1` document to `<Project>.json` and every other document to `<Project>_<name>.json`. Both libraries name their default document `v1`, so when an OpenAPI document has the same name as an AsyncAPI one, the AsyncAPI document is listed as `asyncapi-<name>` instead:

| Document | File |
|----------|------|
| OpenAPI `v1` | `MyApi.json` |
| AsyncAPI `v1` | `MyApi_asyncapi-v1.json` |

To choose the name yourself, set `BuildTimeDocumentName`. It changes only the build-time name; the `/asyncapi/{documentName}.json` endpoint keeps its route:

```csharp
builder.Services.AddOpenApi();
builder.Services.AddAsyncApi(options => options.BuildTimeDocumentName = "events");
// MyApi.json (OpenAPI) and MyApi_events.json (AsyncAPI)
```

Apps that don't call `AddOpenApi()` keep their existing document names and files.

### Arazzo documents

`AddArazzo()` documents are written by the same run. An Arazzo document is JSON, uses the version from
`ArazzoOptions.Version` (1.1.0 by default), and has its serialized-document transformers, such as overlays, applied, so the
file is the one `MapArazzo()` would serve.

It keeps its own name unless an AsyncAPI or OpenAPI document already has it, in which case it is listed as
`arazzo-<name>`. All three libraries name their default document `v1`, so an app that uses them all gets:

| Document | File |
|----------|------|
| OpenAPI `v1` | `MyApi.json` |
| AsyncAPI `v1` | `MyApi_asyncapi-v1.json` |
| Arazzo `v1` | `MyApi_arazzo-v1.json` |

Set `ArazzoOptions.BuildTimeDocumentName` to choose the name yourself. It changes only the build-time name; the
`/arazzo/{documentName}.json` route is not affected:

```csharp
builder.Services.AddArazzo(options => options.BuildTimeDocumentName = "workflows");
// MyApi_workflows.json
```

`AddArazzo()` and `AddAsyncApi()` can be called in either order, and `AddArazzo()` also works in an app that has no
AsyncAPI documents. Only `AddOpenApi()` has to come first, as described above.

The tool starts the app to write the documents, so Arazzo's startup validation runs first. A workflow step that points
at an operation or channel the app doesn't have fails the build with the same
`ArazzoStartupValidationException` it would raise at startup, instead of producing a dangling workflow. Set
`ArazzoOptions.ValidateSourceReferencesOnStartup = false` to skip the check.

Build-time generation writes JSON only; `dotnet getdocument` has no way to ask for YAML. Minimal `WebApplication` apps
should use `Microsoft.Extensions.ApiDescription.Server`, as above: the standalone `dotnet asyncapi getdocument` tool
only supports apps that expose `CreateHostBuilder`.

## Option 2: Using the AsyncAPI CLI Tool

Install the `dotnet asyncapi` CLI tool for more control:

```bash
dotnet tool install -g Bielu.AspNetCore.AsyncApi.Cli
```

Generate documents from a built project:

```bash
dotnet asyncapi getdocument \
    --assembly MyApp \
    --assembly-path bin/Debug/net10.0/MyApp.dll \
    --output ./docs \
    --project MyApp
```

See the [CLI Guide](cli.md) for more information on the `getdocument` command and other CLI features.

## Option 3: Using MSBuild Targets

Add the MSBuild targets package for automatic build-time generation. This package provides MSBuild `.props` and `.targets` files that invoke the `dotnet asyncapi` CLI tool after each build:

```bash
dotnet add package Bielu.AspNetCore.AsyncApi.ApiDescription.Server
dotnet tool install -g Bielu.AspNetCore.AsyncApi.Cli
```

> **Note:** The `Bielu.AspNetCore.AsyncApi.ApiDescription.Server` package depends on the `dotnet asyncapi` CLI tool being installed. Install the CLI tool globally or as a local tool before building.

Configure your `.csproj`:

```xml
<PropertyGroup>
    <AsyncApiGenerateDocumentsOnBuild>true</AsyncApiGenerateDocumentsOnBuild>
    <AsyncApiDocumentsDirectory>$(MSBuildProjectDirectory)/docs</AsyncApiDocumentsDirectory>
</PropertyGroup>
```
