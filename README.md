# Bielu.AspNetCore.AsyncApi

[![CI](https://github.com/bielu/Bielu.AspNetCore.AsyncApi/actions/workflows/ci.yaml/badge.svg)](https://github.com/bielu/Bielu.AspNetCore.AsyncApi/actions/workflows/ci.yaml)
[![NuGet](https://img.shields.io/nuget/v/Bielu.AspNetCore.AsyncApi.svg)](https://www.nuget.org/packages/Bielu.AspNetCore.AsyncApi/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Bielu.AspNetCore.AsyncApi.svg)](https://www.nuget.org/packages/Bielu.AspNetCore.AsyncApi/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

Bielu.AspNetCore.AsyncApi provides built-in support for generating [AsyncAPI](https://www.asyncapi.com/) documents from minimal or controller-based APIs in ASP.NET Core. This library brings the same developer experience as [Microsoft.AspNetCore.OpenApi](https://www.nuget.org/packages/Microsoft.AspNetCore.OpenApi) but for AsyncAPI specifications.

> ⚠️ **Note:** Pre version 1.0.0, the API is regarded as unstable and **breaking changes may be introduced**.

## About

This project is based on and inspired by:
- **[Saunter](https://github.com/asyncapi/saunter)** - The original AsyncAPI documentation generator for .NET
- **[Microsoft.AspNetCore.OpenApi](https://www.nuget.org/packages/Microsoft.AspNetCore.OpenApi)** - Microsoft's OpenAPI implementation that inspired the API design
- **[ByteBard.AsyncAPI.NET](https://www.nuget.org/packages/ByteBard.AsyncAPI.NET/)** - AsyncAPI schema and serialization

## Key Features

- ✅ **Runtime document generation** - View generated AsyncAPI documents at runtime via a parameterized endpoint (`/asyncapi/{documentName}.json`)
- ✅ **Build-time document generation** - Generate AsyncAPI documents at build-time for static hosting
- ✅ **Document transformers** - Customize the generated document via document transformers
- ✅ **Schema transformers** - Customize the generated schema via schema transformers
- ✅ **Protocol bindings** - Support for AMQP, HTTP, MQTT, Kafka, and other protocol bindings
- ✅ **Multiple documents** - Generate multiple AsyncAPI documents from a single application
- ✅ **Interactive UI** - Built-in AsyncAPI UI for exploring your API documentation
- ✅ **Attribute-based configuration** - Decorate your classes with attributes to define channels and operations

## Installation

Install the packages from NuGet:

```bash
# Core package for AsyncAPI document generation
dotnet add package Bielu.AspNetCore.AsyncApi

# Optional: Attributes package for annotating your classes
dotnet add package Bielu.AspNetCore.AsyncApi.Attributes

# Optional: UI package for interactive documentation
dotnet add package Bielu.AspNetCore.AsyncApi.UI
```

## Getting Started

See the [StreetlightsAPI example](./examples/StreetlightsAPI) for a complete working example.

### 1. Configure Services

In your `Program.cs` or `Startup.cs`, configure AsyncAPI services:

```csharp
using Bielu.AspNetCore.AsyncApi.Extensions;
using Bielu.AspNetCore.AsyncApi.UI;

var builder = WebApplication.CreateBuilder(args);

// Add AsyncAPI services
builder.Services.AddAsyncApi(options =>
{
    options.AddServer("mosquitto", "test.mosquitto.org", "mqtt", server =>
    {
        server.Description = "Test Mosquitto MQTT Broker";
    });
    options.AddServer("webapi", "localhost:5000", "http", server =>
    {
        server.Description = "Local HTTP API Server";
    });
    options.WithDefaultContentType("application/json")
        .WithDescription("The Smartylighting Streetlights API allows you to remotely manage the city lights.")
        .WithLicense("Apache 2.0", "https://www.apache.org/licenses/LICENSE-2.0");
});

var app = builder.Build();

// Map the AsyncAPI document endpoint
app.MapAsyncApi();

// Optional: Map the AsyncAPI UI
app.MapAsyncApiUi();

app.Run();
```

### 2. Define Channels with Attributes

Decorate your message bus classes with attributes:

```csharp
using Bielu.AspNetCore.AsyncApi.Attributes.Attributes;

[AsyncApi] // Tells the library to scan this class
public class StreetlightMessageBus : IStreetlightMessageBus
{
    private const string LightMeasuredTopic = "subscribe/light/measured";

    [Channel(LightMeasuredTopic, Servers = new[] { "mosquitto" })]
    [SubscribeOperation(typeof(LightMeasuredEvent), "Light", 
        Summary = "Subscribe to environmental lighting conditions for a particular streetlight.")]
    public void PublishLightMeasurement(LightMeasuredEvent lightMeasuredEvent)
    {
        // Publish message to the channel
    }
}
```

### 3. Access the Generated Document

Once configured, access your AsyncAPI document:

- **JSON Document**: `GET /asyncapi/asyncapi.json`
- **AsyncAPI UI**: `GET /asyncapi/ui/`

```json
{
  "asyncapi": "2.6.0",
  "info": {
    "title": "Streetlights API",
    "version": "1.0.0",
    "description": "The Smartylighting Streetlights API allows you to remotely manage the city lights."
  },
  "channels": {
    "subscribe/light/measured": {
      "subscribe": {
        "operationId": "PublishLightMeasurement",
        "summary": "Subscribe to environmental lighting conditions for a particular streetlight."
      }
    }
  }
}
```

![AsyncAPI UI](./assets/asyncapi-ui-screenshot.png)

## Main Types

The main types provided by this library are:

| Type | Description |
|------|-------------|
| `AsyncApiOptions` | Options for configuring AsyncAPI document generation |
| `IDocumentTransformer` | Interface for transformers that modify the generated AsyncAPI document |
| `ISchemaTransformer` | Interface for transformers that modify generated schemas |
| `AsyncApiAttribute` | Marks a class for scanning by the AsyncAPI generator |
| `ChannelAttribute` | Defines a channel on a method |
| `SubscribeOperationAttribute` | Defines a subscribe operation on a channel |
| `PublishOperationAttribute` | Defines a publish operation on a channel |
| `MessageAttribute` | Defines message metadata |

## Configuration Options

Configure the library with the fluent API:

```csharp
builder.Services.AddAsyncApi(options =>
{
    // Add servers
    options.AddServer("production", "api.example.com", "amqp");
    
    // Add protocol bindings
    options.AddChannelBinding("amqpDev", new AMQPChannelBinding
    {
        Is = ChannelType.Queue,
        Queue = new Queue { Name = "example-queue", Vhost = "/development" }
    });
    
    options.AddOperationBinding("postBind", new HttpOperationBinding
    {
        Method = "POST",
        Type = HttpOperationType.Response
    });
    
    // Configure document metadata
    options.WithDefaultContentType("application/json")
        .WithDescription("My API Description")
        .WithLicense("MIT", "https://opensource.org/licenses/MIT");
    
    // Add transformers
    options.AddDocumentTransformer<MyCustomTransformer>();
    options.AddSchemaTransformer<MySchemaTransformer>();
});
```

## Version-Specific Requirements

### AsyncAPI 2.x

When using AsyncAPI 2.x specification (`AsyncApiVersion.AsyncApi2_0`), **at least one channel must be defined**. The AsyncAPI 2.x specification requires the `channels` field to be present and non-empty.

If your application doesn't define any channels (e.g., for documentation purposes only), you have two options:

1. **Use AsyncAPI 3.x** (recommended) - AsyncAPI 3.x does not require channels:
   ```csharp
   builder.Services.AddAsyncApi(options =>
   {
       options.AsyncApiVersion = AsyncApiVersion.AsyncApi3_0; // Default
       options.WithInfo("My API", "1.0.0");
       // No channels required
   });
   ```

2. **Define at least one channel** for AsyncAPI 2.x:
   ```csharp
   builder.Services.AddAsyncApi(options =>
   {
       options.AsyncApiVersion = AsyncApiVersion.AsyncApi2_0;
       options.WithInfo("My API", "1.0.0");
       // Must have at least one channel defined via attributes
   });
   ```

> ⚠️ **Note:** Documents generated with AsyncAPI 2.x without any channels will fail validation. This is a requirement of the AsyncAPI 2.x specification, not a limitation of this library.

### AsyncAPI 3.x

AsyncAPI 3.x (default) does not require channels. This version is recommended for most new projects as it provides more flexibility and supports the latest AsyncAPI features including AsyncAPI 3.1 schema generation.

## Protocol Bindings

Bindings describe protocol-specific information. Apply them to channels and operations using the `BindingsRef` property:

```csharp
// Configure bindings
builder.Services.AddAsyncApi(options =>
{
    options.AddChannelBinding("amqpDev", new AMQPChannelBinding
    {
        Is = ChannelType.Queue,
        Queue = new Queue { Name = "example-queue", Vhost = "/development" }
    });
});

// Use bindings in attributes
[Channel("light.measured", BindingsRef = "amqpDev")]
[SubscribeOperation(typeof(LightMeasuredEvent), "Light")]
public void PublishLightMeasuredEvent(LightMeasuredEvent lightMeasuredEvent) { }
```

Available bindings: [AsyncAPI.NET.Bindings](https://www.nuget.org/packages/AsyncAPI.NET.Bindings/)

## Multiple AsyncAPI Documents

Generate multiple AsyncAPI documents by specifying document names:

```csharp
// Configure named documents
builder.Services.AddAsyncApi("internal", options =>
{
    options.WithDescription("Internal API");
});

builder.Services.AddAsyncApi("public", options =>
{
    options.WithDescription("Public API");
});
```

Decorate classes with the document name:

```csharp
[AsyncApi("internal")]
public class InternalMessageBus { }

[AsyncApi("public")]
public class PublicMessageBus { }
```

Access documents by name:
- `GET /asyncapi/internal/asyncapi.json`
- `GET /asyncapi/public/asyncapi.json`

## Migration from Saunter

If you're migrating from the original Saunter library, note these changes:

### Namespace Changes
| Old | New |
|-----|-----|
| `Saunter.AsyncApiSchema.v2` | `ByteBard.AsyncAPI.Models` |
| `Saunter.Attributes` | `Bielu.AspNetCore.AsyncApi.Attributes.Attributes` |

### API Changes
- Data structure names now have an `AsyncApi` prefix (e.g., `Info` → `AsyncApiInfo`)
- All data structure constructors are now parameterless
- The service registration method has changed from `AddAsyncApiSchemaGeneration` to `AddAsyncApi`

### Example Migration

**Before (Saunter):**
```csharp
services.AddAsyncApiSchemaGeneration(options =>
{
    options.AssemblyMarkerTypes = new[] { typeof(MyMessageBus) };
    options.AsyncApi = new AsyncApiDocument { ... };
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
    options.AddServer("mosquitto", "test.mosquitto.org", "mqtt");
    options.WithDescription("My API");
});

app.MapAsyncApi();
app.MapAsyncApiUi();
```

## Contributing

We welcome contributions! See our [Contributing Guide](./CONTRIBUTING.md) for details on:
- Setting up your development environment
- Building and testing the project
- Submitting pull requests

Feel free to get involved by opening issues or submitting pull requests.

## Acknowledgments

This project builds upon the excellent work of:

- **[Saunter](https://github.com/asyncapi/saunter)** - The original AsyncAPI documentation generator for .NET, maintained by the AsyncAPI Initiative
- **[Microsoft.AspNetCore.OpenApi](https://github.com/dotnet/aspnetcore)** - Microsoft's OpenAPI implementation that inspired the fluent API design
- **[ByteBard.AsyncAPI.NET](https://www.nuget.org/packages/ByteBard.AsyncAPI.NET/)** - AsyncAPI schema models and serialization (fork of LEGO AsyncAPI.NET)
- **[Swashbuckle](https://github.com/domaindrivendev/Swashbuckle.AspNetCore)** - The OpenAPI/Swagger implementation that inspired the original Saunter project

## License

This project is licensed under the MIT License - see the [LICENSE](./LICENSE) file for details.
