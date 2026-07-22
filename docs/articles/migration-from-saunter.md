# Migration from Saunter

If you're migrating from the original [Saunter](https://github.com/asyncapi/saunter) library, follow this guide to update your codebase to use `Bielu.AspNetCore.AsyncApi`.

## Namespace Changes

| Old | New |
|-----|-----|
| `Saunter.AsyncApiSchema.v2` | `ByteBard.AsyncAPI.Models` |
| `Saunter.Attributes` | `Bielu.AspNetCore.AsyncApi.Attributes.Attributes` |

## API Changes

- **Data structure names**: Most names now have an `AsyncApi` prefix (e.g., `Info` → `AsyncApiInfo`, `Server` → `AsyncApiServer`).
- **Constructors**: All data structure constructors are now parameterless. Use object initializers to set properties.
- **Registration**: The service registration method has changed from `AddAsyncApiSchemaGeneration` to `AddAsyncApi`.
- **Mapping**: Use `app.MapAsyncApi()` instead of `endpoints.MapAsyncApiDocuments()`.
- **UI**: Saunter's built-in UI has been removed. Use `Scalar.AspNetCore` instead.

## Example Migration

### Before (Saunter)

```csharp
services.AddAsyncApiSchemaGeneration(options =>
{
    options.AssemblyMarkerTypes = new[] { typeof(MyMessageBus) };
    options.AsyncApi = new AsyncApiDocument 
    { 
        Info = new Info("My API", "1.0.0") 
    };
});

app.UseEndpoints(endpoints =>
{
    endpoints.MapAsyncApiDocuments();
    endpoints.MapAsyncApiUi();
});
```

### After (Bielu.AspNetCore.AsyncApi)

```csharp
builder.Services.AddAsyncApi(options =>
{
    options.WithInfo("My API", "1.0.0");
    options.AddServer("mosquitto", "test.mosquitto.org", "mqtt");
});

var app = builder.Build();

app.MapAsyncApi();

// Scalar UI replacement
app.MapScalarApiReference(options =>
{
    options.AddAsyncApiDocument("v1", "My API", "/asyncapi/v1.json");
});
```
