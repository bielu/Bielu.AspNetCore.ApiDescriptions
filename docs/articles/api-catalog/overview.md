# API catalog

`Bielu.AspNetCore.ApiCatalog` serves an [RFC 9727](https://www.rfc-editor.org/rfc/rfc9727) API catalog: a single
well-known URL, `/.well-known/api-catalog`, that lists every API description the app serves. A client, an API
gateway or a crawler can then find the AsyncAPI, OpenAPI and Arazzo documents without knowing their routes.

```sh
dotnet add package Bielu.AspNetCore.ApiCatalog
```

```csharp
builder.Services.AddOpenApi();
builder.Services.AddAsyncApi();
builder.Services.AddArazzo(options => { /* workflows */ });

var app = builder.Build();
app.MapOpenApi();
app.MapAsyncApi();
app.MapArazzo();
app.MapApiCatalog();
```

## What the catalog lists

The catalog is an [RFC 9264](https://www.rfc-editor.org/rfc/rfc9264) Linkset, served as
`application/linkset+json; profile="https://www.rfc-editor.org/info/rfc9727"`. It has two link contexts:

- the catalog itself, whose `item` links are the APIs: this app (its `ApiUrl`, the base URL by default), then every
  `AddItem(...)` entry;
- this app's API, with one `service-desc` link per document and one `service-doc` link per `AddServiceDoc(...)` entry.

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
        { "href": "https://localhost:5001/asyncapi/v1.json", "type": "application/vnd.aai.asyncapi+json", "title": "AsyncAPI document 'v1'" },
        { "href": "https://localhost:5001/arazzo/v1.json", "type": "application/vnd.oai.workflows+json", "title": "Arazzo document 'v1'" }
      ]
    }
  ]
}
```

Documents are found from the app's own endpoints, in the order they were mapped:

| Endpoint | Media type | Documents |
| --- | --- | --- |
| `MapAsyncApi()` | `application/vnd.aai.asyncapi+json` (`+yaml` for a `.yaml`/`.yml` route) | every `AddAsyncApi` document |
| `MapArazzo()` | `application/vnd.oai.workflows+json` (`+yaml`) | every `AddArazzo` document |
| `MapOpenApi()` | `application/vnd.oai.openapi+json` (`+yaml`) | every `AddOpenApi` document |

`MapAsyncApi()` and `MapArazzo()` mark their endpoints with `ApiDescriptionDocumentMetadata`. `MapOpenApi()` leaves no
marker, so its endpoint is recognized by its route: if you give it a route other than `/openapi/{documentName}.json`,
set the same route on `ApiCatalogOptions.OpenApiRoutePattern`, or set it to `null` to leave OpenAPI documents out.

To list documents from an endpoint of your own, add the same metadata to it. The kind has to match the `Kind` of the
`IBuildTimeDocumentSource` that owns those documents:

```csharp
app.MapGet("/workflows/{documentName}.json", handler)
    .WithMetadata(new ApiDescriptionDocumentMetadata("Workflow", "application/json"));
```

## Options

```csharp
builder.Services.AddApiCatalog(options => options
    .AddServiceDoc("/scalar", "API reference")
    .AddItem("https://payments.example.com/.well-known/api-catalog", "Payments"));
```

| Member | Effect |
| --- | --- |
| `AddServiceDoc(href, title)` | Adds a `service-doc` link (RFC 8631) to human-readable documentation such as the Scalar page. |
| `AddItem(href, title)` | Adds an `item` link: another API, or another catalog to nest. |
| `OpenApiRoutePattern` | The route `MapOpenApi()` was given. `null` leaves OpenAPI documents out. |
| `ApiUrl` | Where this app's API lives: its `item` link, and the anchor of its `service-desc`/`service-doc` links. Defaults to `/`; set it (for example to `/api`) when the app root serves nothing. |

`AddApiCatalog()` is optional; `MapApiCatalog()` works with the defaults.

Paths such as `/scalar` are resolved against the app's base URL. Every link in the catalog is absolute, built from the
request's scheme, host and path base, so behind a reverse proxy apply the forwarded headers
(`app.UseForwardedHeaders()`) before the catalog endpoint.

## HTTP behavior

- `GET` returns the catalog.
- `HEAD` returns the same headers without a body, as RFC 9727 requires.
- Both carry `Link: <…/.well-known/api-catalog>; rel="api-catalog"`.

`MapApiCatalog()` returns the endpoint's convention builder, so you can restrict access to an internal catalog as the
RFC recommends:

```csharp
app.MapApiCatalog().RequireAuthorization("internal");
```
