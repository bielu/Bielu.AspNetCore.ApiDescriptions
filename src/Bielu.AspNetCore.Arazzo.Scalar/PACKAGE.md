# Bielu.AspNetCore.Arazzo.Scalar

Renders [Arazzo](https://spec.openapis.org/arazzo/latest.html) workflows in the
[Scalar](https://scalar.com/) API Reference in ASP.NET Core. Each workflow is listed in the sidebar
with its steps. Every step shows the operation or channel it calls, resolved in the OpenAPI and
AsyncAPI documents loaded next to it, along with its parameters, success criteria, actions and
outputs.

## Installation

```sh
dotnet add package Bielu.AspNetCore.Arazzo.Scalar
dotnet add package Scalar.AspNetCore
```

## Usage

```csharp
app.MapArazzo();
app.MapScalarArazzoAssets();

app.MapScalarApiReference(options =>
{
    options.AddDocument("v1");                          // the OpenAPI document the steps call
    options.AddAsyncApiDocument("v1", "Events");        // ...and/or an AsyncAPI one
    options.AddArazzoDocument("v1", "Workflows");       // served by MapArazzo()
    options.WithArazzo();
});
```

> **Requires a Scalar version with plugin document types**
> ([scalar/scalar#10816](https://github.com/scalar/scalar/pull/10816)). Older versions ignore the
> plugin and show an Arazzo source as an empty OpenAPI document.

## Documentation

- [Arazzo in Scalar](https://apidescriptions.bielu.pl/articles/arazzo/scalar.html)
- [Full documentation](https://apidescriptions.bielu.pl/)

Copyright (c) 2025 Arkadiusz Biel. Released under the [MIT license](https://licenses.nuget.org/MIT).
