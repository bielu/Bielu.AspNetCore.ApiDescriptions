# Bielu.AspNetCore.ApiCatalog

Serves an [RFC 9727](https://www.rfc-editor.org/rfc/rfc9727) **API catalog** at
`/.well-known/api-catalog`: one well-known URL listing every API description your app serves, so a
client, gateway or crawler can find them without knowing their URLs in advance.

The catalog is an [RFC 9264](https://www.rfc-editor.org/rfc/rfc9264) Linkset. Each document is a
`service-desc` link with its media type, discovered from the app's own endpoints:

- every document of `MapAsyncApi()` (`application/vnd.aai.asyncapi+json`)
- every document of `MapArazzo()` (`application/vnd.oai.workflows+json`)
- every document of `MapOpenApi()` (`application/vnd.oai.openapi+json`)

## Installation

```sh
dotnet add package Bielu.AspNetCore.ApiCatalog
```

## Usage

```csharp
builder.Services.AddOpenApi();
builder.Services.AddAsyncApi();
builder.Services.AddApiCatalog(options => options
    .AddServiceDoc("/scalar", "API reference")                       // service-doc link
    .AddItem("https://payments.example.com/.well-known/api-catalog")); // another API or catalog

var app = builder.Build();
app.MapOpenApi();
app.MapAsyncApi();
app.MapApiCatalog(); // GET and HEAD /.well-known/api-catalog
```

```json
{
  "linkset": [
    {
      "anchor": "https://localhost:5001/.well-known/api-catalog",
      "item": [ { "href": "https://localhost:5001/" } ]
    },
    {
      "anchor": "https://localhost:5001/",
      "service-desc": [
        { "href": "https://localhost:5001/openapi/v1.json", "type": "application/vnd.oai.openapi+json", "title": "OpenAPI document 'v1'" },
        { "href": "https://localhost:5001/asyncapi/v1.json", "type": "application/vnd.aai.asyncapi+json", "title": "AsyncAPI document 'v1'" }
      ]
    }
  ]
}
```

`AddApiCatalog()` is optional; call it only to change the defaults. If `MapOpenApi()` uses a
non-default route, set `OpenApiRoutePattern` to it, because OpenAPI's endpoint is recognized by its
route.

## Documentation

- [API catalog](https://apidescriptions.bielu.pl/articles/api-catalog/overview.html)
- [Full documentation](https://apidescriptions.bielu.pl/)

## Feedback & Contributing

Copyright (c) 2025 Arkadiusz Biel. Released under the [MIT license](https://licenses.nuget.org/MIT).
Bug reports and contributions are welcome at [the GitHub repository](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions).
