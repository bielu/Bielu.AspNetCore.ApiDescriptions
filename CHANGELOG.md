# Changelog

All notable changes to this project will be documented in this file.

This project is a fork/evolution of [Saunter](https://github.com/asyncapi/saunter), the original AsyncAPI documentation generator for .NET. Below you'll find both the version history for Bielu.AspNetCore.AsyncApi and a comparison of changes from the original Saunter library.

## [Unreleased]

### Added

- **SignalR protocol bindings** - New `Bielu.AspNetCore.AsyncApi.Extensions.Protocols.SignalR` package
  providing a custom `signalr` protocol with channel, operation, message and server bindings, plus a
  runnable `SignalRChat` example.
- **gRPC protocol bindings** - New `Bielu.AspNetCore.AsyncApi.Extensions.Protocols.Grpc` package
  providing a custom `grpc` protocol with channel, operation, message and server bindings.
- **Server-Sent Events (SSE) protocol bindings** - New `Bielu.AspNetCore.AsyncApi.Extensions.Protocols.Sse`
  package providing a custom `sse` protocol with channel, operation, message and server bindings
  modelling the `text/event-stream` (`event`/`id`/`retry`/`data`) wire shape.
- **WebRTC protocol bindings** - New `Bielu.AspNetCore.AsyncApi.Extensions.Protocols.WebRtc` package
  providing a custom `webrtc` protocol with channel, operation, message and server bindings covering
  `RTCDataChannel` streams and SDP/ICE signaling.

### Fixed

- `BindingsRef` on `[Channel]` and operation attributes now actually attaches the referenced binding
  (registered via `AddChannelBinding`/`AddOperationBinding`) to the channel/operation in the generated
  document. Previously the binding was only stored under `components` and never linked.

## Changes from Saunter

This section documents the key differences between Bielu.AspNetCore.AsyncApi and the original [Saunter](https://github.com/asyncapi/saunter) library.

### New Features

- **Fluent Configuration API** - New `AddAsyncApi()` method with fluent builder pattern inspired by Microsoft.AspNetCore.OpenApi
  ```csharp
  // New fluent API
  builder.Services.AddAsyncApi(options =>
  {
      options.AddServer("mosquitto", "test.mosquitto.org", "mqtt");
      options.WithDescription("My API");
      options.WithLicense("MIT", "https://opensource.org/licenses/MIT");
  });
  ```

- **Document Transformers** - Support for `IDocumentTransformer` to customize the generated AsyncAPI document
- **Schema Transformers** - Support for `ISchemaTransformer` to customize generated schemas
- **Separate UI Package** - `Bielu.AspNetCore.AsyncApi.UI` as a standalone package with modern AsyncAPI React components
- **Separate Attributes Package** - `Bielu.AspNetCore.AsyncApi.Attributes` for annotation-only scenarios
- **.NET 10 Support** - Updated to target .NET 10

### Breaking Changes from Saunter

#### Namespace Changes

| Saunter (Old) | Bielu.AspNetCore.AsyncApi (New) |
|---------------|----------------------------------|
| `Saunter.AsyncApiSchema.v2` | `ByteBard.AsyncAPI.Models` |
| `Saunter.Attributes` | `Bielu.AspNetCore.AsyncApi.Attributes.Attributes` |
| `Saunter` | `Bielu.AspNetCore.AsyncApi.Extensions` |

#### API Changes

| Saunter (Old) | Bielu.AspNetCore.AsyncApi (New) |
|---------------|----------------------------------|
| `AddAsyncApiSchemaGeneration()` | `AddAsyncApi()` |
| `MapAsyncApiDocuments()` | `MapAsyncApi()` |
| `options.AssemblyMarkerTypes` | Auto-discovery via attributes |
| `options.AsyncApi = new AsyncApiDocument {...}` | Fluent builder: `options.AddServer()`, `options.WithDescription()` |

#### Data Structure Changes

- All data structure names now have an `AsyncApi` prefix:
  - `Info` → `AsyncApiInfo`
  - `Server` → `AsyncApiServer`
  - `License` → `AsyncApiLicense`
  - `Contact` → `AsyncApiContact`
- All data structure constructors are now parameterless

#### Dependency Changes

| Saunter | Bielu.AspNetCore.AsyncApi |
|---------|---------------------------|
| LEGO AsyncAPI.NET | ByteBard.AsyncAPI.NET |
| AsyncAPI.NET.Bindings | AsyncAPI.NET.Bindings (same) |

### Migration Example

**Before (Saunter):**
```csharp
services.AddAsyncApiSchemaGeneration(options =>
{
    options.AssemblyMarkerTypes = new[] { typeof(MyMessageBus) };
    options.AsyncApi = new AsyncApiDocument
    {
        Info = new Info("My API", "1.0.0"),
        Servers = 
        {
            ["mqtt"] = new Server("broker.example.com", "mqtt")
        }
    };
});

app.UseEndpoints(endpoints =>
{
    endpoints.MapAsyncApiDocuments();
    endpoints.MapAsyncApiUi();
});
```

**After (Bielu.AspNetCore.AsyncApi):**
```csharp
builder.Services.AddAsyncApi(options =>
{
    options.AddServer("mqtt", "broker.example.com", "mqtt");
    options.WithTitle("My API")
        .WithVersion("1.0.0");
});

app.MapAsyncApi();
app.MapAsyncApiUi();
```

### Package Comparison

| Feature | Saunter | Bielu.AspNetCore.AsyncApi |
|---------|---------|---------------------------|
| NuGet Package | `Saunter` | `Bielu.AspNetCore.AsyncApi` |
| Attributes Package | Included | `Bielu.AspNetCore.AsyncApi.Attributes` |
| UI Package | Included | `Bielu.AspNetCore.AsyncApi.UI` |
| Target Framework | .NET 6+ | .NET 10 |
| AsyncAPI Version | 2.x | 2.6.0 |
| Configuration Style | Object initialization | Fluent API |
| Document Transformers | Filters | Transformers |

## Version History

### v1.0.0 (Upcoming)

Initial release of Bielu.AspNetCore.AsyncApi with the following features:

- Complete rewrite of configuration API with fluent builder pattern
- Separated packages for core, attributes, and UI
- Document and schema transformers
- Updated to ByteBard.AsyncAPI.NET for schema handling
- .NET 10 support
- Improved endpoint routing with `MapAsyncApi()` and `MapAsyncApiUi()`

---

## Attribution

This project is based on [Saunter](https://github.com/asyncapi/saunter) by the AsyncAPI Initiative and draws inspiration from [Microsoft.AspNetCore.OpenApi](https://github.com/dotnet/aspnetcore) for its API design.