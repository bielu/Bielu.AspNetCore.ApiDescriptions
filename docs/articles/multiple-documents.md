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
