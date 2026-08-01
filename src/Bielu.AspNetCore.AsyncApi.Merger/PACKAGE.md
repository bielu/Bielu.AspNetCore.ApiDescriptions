# Bielu.AspNetCore.AsyncApi.Merger

Merges AsyncAPI documents from several services into one document — the usual shape for an API
gateway that wants to present a single description of everything behind it.

## Installation

```sh
dotnet add package Bielu.AspNetCore.AsyncApi.Merger
```

## Usage

```csharp
builder.Services.AddAsyncApiMerge(options =>
{
    options.AddSource("https://orders.internal/asyncapi/v1.json", keyPrefix: "orders");
    options.AddSource("https://inventory.internal/asyncapi/v1.json", keyPrefix: "inventory");
});

var app = builder.Build();

app.MapMergedAsyncApi("/asyncapi/v1.json");
```

Sources may be local files or remote URLs. Remote documents are cached and re-fetched on change, and
channel names can be prefixed per source so two services publishing the same channel do not collide.

## Documentation

- [Merger & gateway](https://apidescriptions.bielu.pl/articles/merger-gateway.html)
- [Full documentation](https://apidescriptions.bielu.pl/)

## Feedback & Contributing

Released under the [MIT license](https://licenses.nuget.org/MIT). Bug reports and contributions are
welcome at [the GitHub repository](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions).
