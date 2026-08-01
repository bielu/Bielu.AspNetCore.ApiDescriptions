# Bielu.AspNetCore.AsyncApi.Overlay

Applies [OpenAPI Overlays](https://spec.openapis.org/overlay/latest.html) to your AsyncAPI document
**inside the generation pipeline**, so the document served from `/asyncapi/{documentName}.json` is
already transformed. No build step, and no second artifact to keep in sync.

An overlay is a small, reviewable JSON/YAML file of ordered actions that select nodes by JSONPath and
then update, copy or remove them — the declarative alternative to writing transformation code in C#.

## Installation

```sh
dotnet add package Bielu.AspNetCore.AsyncApi.Overlay
```

## Usage

```csharp
builder.Services.AddAsyncApi("v1", options =>
{
    options.AddOverlay("overlays/public.yaml");     // applied in declaration order
    options.ConfigureOverlays(o => o.Strict = true); // a zero-match target becomes an error
});
```

Overlays apply at **both** production points — the runtime endpoint (JSON and YAML) and build-time
generation — so a checked-in document and the served one can never disagree about whether the overlay
ran. Overlay files are resolved lazily and cached, so a missing file cannot break startup, and a
failure surfaces as an error rather than a silently untransformed document.

## Documentation

- [Overlay pipeline integration](https://apidescriptions.bielu.pl/articles/overlay/pipeline-integration.html)
- [Full documentation](https://apidescriptions.bielu.pl/)

## Feedback & Contributing

Released under the [MIT license](https://licenses.nuget.org/MIT). Bug reports and contributions are
welcome at [the GitHub repository](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions).
