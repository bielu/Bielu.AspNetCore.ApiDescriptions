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

`AddAsyncApi()` makes build-time generation serve both libraries' documents by redirecting the document provider that `AddOpenApi()` has already registered. An `AddOpenApi()` call made afterwards registers that provider again, so depending on which provider `dotnet getdocument` picks, the AsyncAPI documents can be missing. The **BASYNC010** analyzer warns when `AddOpenApi()` follows `AddAsyncApi()` (or `AddAsyncApiForApiVersions()`) in the same method. It can't compare calls made from different methods, so keep both registrations in one place, such as `Program.cs`.

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
