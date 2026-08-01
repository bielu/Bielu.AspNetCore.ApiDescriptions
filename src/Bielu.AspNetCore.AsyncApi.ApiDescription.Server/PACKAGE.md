# Bielu.AspNetCore.AsyncApi.ApiDescription.Server

MSBuild targets that generate your AsyncAPI document **at build time**, so it can be committed to the
repository, reviewed in a pull request, and diffed in CI.

The equivalent of `Microsoft.Extensions.ApiDescription.Server` for AsyncAPI.

## Installation

```sh
dotnet add package Bielu.AspNetCore.AsyncApi.ApiDescription.Server
```

## Usage

Referencing the package is enough — the document is written on build. Control it with MSBuild
properties:

```xml
<PropertyGroup>
  <AsyncApiGenerateDocuments>true</AsyncApiGenerateDocuments>
  <AsyncApiDocumentsDirectory>$(MSBuildProjectDirectory)/asyncapi</AsyncApiDocumentsDirectory>
</PropertyGroup>
```

Generation runs the same pipeline the runtime endpoint uses, so a checked-in document and the one
served from `/asyncapi/{documentName}.json` cannot disagree.

## Documentation

- [Build-time generation](https://apidescriptions.bielu.pl/articles/build-time-generation.html)
- [Full documentation](https://apidescriptions.bielu.pl/)

## Feedback & Contributing

Released under the [MIT license](https://licenses.nuget.org/MIT). Bug reports and contributions are
welcome at [the GitHub repository](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions).
