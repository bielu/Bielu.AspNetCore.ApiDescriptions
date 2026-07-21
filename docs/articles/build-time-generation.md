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

AsyncAPI documents will be generated during `dotnet build` using the `IDocumentProvider` interface registered by `AddAsyncApi()`.

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
