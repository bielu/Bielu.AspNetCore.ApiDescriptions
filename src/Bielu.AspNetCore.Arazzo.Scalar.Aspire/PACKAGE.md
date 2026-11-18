# Bielu.AspNetCore.Arazzo.Scalar.Aspire

Renders [Arazzo](https://spec.openapis.org/arazzo/latest.html) workflows in a
[Scalar.Aspire](https://www.nuget.org/packages/Scalar.Aspire) API Reference resource from your .NET Aspire
AppHost, next to the OpenAPI and AsyncAPI documents their steps call.

## Installation

```sh
dotnet add package Bielu.AspNetCore.Arazzo.Scalar.Aspire
```

## Usage

```csharp
var api = builder.AddProject<Projects.MyApi>("api");   // serves /arazzo/v1.json with MapArazzo()

builder.AddScalarApiReference()
    .WithArazzo()
    .WithApiReference(api, options => options
        .AddDocument("v1")                              // the OpenAPI document the steps call
        .AddArazzoDocument("v1", "Workflows"));         // /arazzo/{documentName}.json by default
```

`WithArazzo` registers the `@bielu/scalar-arazzo` plugin through Scalar's `PluginUrls` option, which
loads it as an ES module before the API Reference mounts. The Scalar container keeps its own bundle and
only the plugin is added, so it combines with the SignalR and gRPC consoles on the same resource.

By default the plugin is loaded from jsDelivr, pinned to the exact npm version released alongside this
package. Pass `pluginUrl:` to load it from somewhere else.

> **Requires `@scalar/api-reference` 1.80.0 or later in the Scalar container image**, the first release with
> plugin document types. Older images show an Arazzo source as an empty OpenAPI document.

## Documentation

- [Arazzo in Scalar](https://apidescriptions.bielu.pl/articles/arazzo/scalar.html)
- [Full documentation](https://apidescriptions.bielu.pl/)

## Feedback & Contributing

Copyright (c) 2025 Arkadiusz Biel. Released under the [MIT license](https://licenses.nuget.org/MIT).
Bug reports and contributions are welcome at [the GitHub repository](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions).
