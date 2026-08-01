# Bielu.AspNetCore.Arazzo.Overlay

Applies [OpenAPI Overlays](https://spec.openapis.org/overlay/latest.html) to your
[Arazzo](https://spec.openapis.org/arazzo/latest.html) workflow documents inside the generation
pipeline, so the document served by `MapArazzo()` is already transformed.

The Arazzo counterpart of
[Bielu.AspNetCore.AsyncApi.Overlay](https://www.nuget.org/packages/Bielu.AspNetCore.AsyncApi.Overlay),
with which it shares its pipeline.

## Installation

```sh
dotnet add package Bielu.AspNetCore.Arazzo.Overlay
```

## Usage

```csharp
builder.Services.AddArazzo(options =>
{
    options.AddOverlay("overlays/public.yaml");
});
```

Multiple overlays apply in declaration order and share a single parse/serialize round trip.

## Documentation

- [Overlay pipeline integration](https://apidescriptions.bielu.pl/articles/overlay/pipeline-integration.html)
- [Full documentation](https://apidescriptions.bielu.pl/)

## Feedback & Contributing

Released under the [MIT license](https://licenses.nuget.org/MIT). Bug reports and contributions are
welcome at [the GitHub repository](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions).
