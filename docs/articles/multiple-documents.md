# Multiple AsyncAPI Documents

Bielu.AspNetCore.AsyncApi supports generating multiple AsyncAPI documents from a single ASP.NET Core application. This is useful for separating internal and public APIs, or for documenting different microservices managed by the same process.

## 1. Register Named Documents

Specifying a name in `AddAsyncApi` creates a named document configuration.

```csharp
// Configure "public" document
builder.Services.AddAsyncApi("public", options =>
{
    options.WithInfo("Public API", "1.0.0")
           .WithDescription("API for external consumers");
});

// Configure "internal" document
builder.Services.AddAsyncApi("internal", options =>
{
    options.WithInfo("Internal API", "1.0.0")
           .WithDescription("API for internal services only");
});
```

## 2. Associate Classes with Documents

Use the `[AsyncApi]` attribute on your classes to specify which document they belong to. If no name is specified, the class is included in the default document (usually "v1").

```csharp
[AsyncApi("public")]
public class PublicMessageBus { ... }

[AsyncApi("internal")]
public class InternalMessageBus { ... }
```

## 3. Map Named Endpoints

`app.MapAsyncApi()` will automatically expose all registered documents. By default, they are available at:

- `GET /asyncapi/public/asyncapi.json`
- `GET /asyncapi/internal/asyncapi.json`

## 4. Viewing Multiple Documents in Scalar

Scalar can render multiple documents in a single UI by adding them to the options.

```csharp
app.MapScalarApiReference(options =>
{
    options.AddAsyncApiDocument("public", "Public API", "/asyncapi/public/asyncapi.json")
           .AddAsyncApiDocument("internal", "Internal API", "/asyncapi/internal/asyncapi.json");
});
```

## 5. One Document per API Version

With [Asp.Versioning](https://github.com/dotnet/aspnet-api-versioning), `Bielu.AspNetCore.AsyncApi.Versioning`
registers one document per API version, named after the API explorer's group names:

```csharp
builder.Services.AddApiVersioning().AddApiExplorer(options => options.GroupNameFormat = "'v'V");
builder.Services.AddAsyncApiForApiVersions();

app.MapAsyncApi();   // -> /asyncapi/v1.json, /asyncapi/v2.json, ...
```

Each version's document holds only the declarations of that version:

```csharp
[AsyncApi]
[ApiVersion("1.0")]
[ApiVersion("2.0")]
public class OrderEvents
{
    [Channel("orders")]
    [SendOperation(typeof(OrderPlaced))]
    public void Placed() { }                  // in v1 and v2

    [Channel("orders")]
    [SendOperation(typeof(OrderPlacedV2), OperationId = "placedV2")]
    [MapToApiVersion("2.0")]
    public void PlacedV2() { }                // in v2 only: v1's "orders" channel never sees OrderPlacedV2

    [Channel("health")]
    [ApiVersionNeutral]
    public void Health() { }                  // in every version
}
```

- A controller action or minimal API handler that Asp.Versioning has versioned follows its endpoint's
  `ApiVersionMetadata`, so versions applied by conventions (`options.Conventions.Controller<T>().HasApiVersion(...)`,
  `.HasApiVersion(...)` on a versioned minimal API) count as well as attributes. A class-level channel
  on a controller is in a version when at least one of its actions is. An unversioned controller in an
  app with `AssumeDefaultVersionWhenUnspecified` is mapped by Asp.Versioning to the default version, so
  it shows up only in that version's document.
- Any other `[AsyncApi]` class or method follows its attributes. A method's own `[ApiVersionNeutral]`,
  `[MapToApiVersion]` or `[ApiVersion]` wins over its class's; a method with none of them takes the class's
  versions. `[AdvertiseApiVersions]` is ignored, since it names versions implemented elsewhere. Custom
  attributes implementing `IApiVersionProvider` or `IApiVersionNeutral` work too.
- Version-neutral declarations, and declarations with no version information at all, are in every
  version's document.
- Declarations are filtered before their channels, messages, schemas and operations are built, so a
  channel declared by several versions carries only each version's own messages and operations, and
  class-level declarations never bring back a method of another version. This works with the
  source-generated metadata provider as well as the reflection-based one, and build-time generation
  produces the same documents as the running app.
- `[AsyncApi("name")]` and `AddIncludedChannel(...)` still apply on top, so the `configure` callback
  can narrow a version's document further:

  ```csharp
  builder.Services.AddAsyncApiForApiVersions((options, description) =>
  {
      if (description.ApiVersion.MajorVersion == 1)
      {
          options.AddIncludedChannel("orders");
      }
  });
  ```

Channels added by document transformers or other integrations, such as [Wolverine](wolverine.md),
are not filtered by version.
