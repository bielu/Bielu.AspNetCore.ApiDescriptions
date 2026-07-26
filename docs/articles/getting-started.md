# Getting Started

Bielu.AspNetCore.AsyncApi makes it easy to document your event-driven APIs in ASP.NET Core using a familiar API similar to OpenAPI.

## Installation

Install the core package and optionally the attributes package:

```bash
# Core package for AsyncAPI document generation
dotnet add package Bielu.AspNetCore.AsyncApi

# Optional: Attributes package for annotating your classes
dotnet add package Bielu.AspNetCore.AsyncApi.Attributes

# Optional: Scalar UI for interactive documentation (recommended)
dotnet add package Scalar.AspNetCore
```

## Basic Configuration

In your `Program.cs`, add the AsyncAPI services and map the document endpoint.

```csharp
using Bielu.AspNetCore.AsyncApi.Extensions;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add AsyncAPI services
builder.Services.AddAsyncApi(options =>
{
    options.AddServer("mosquitto", "test.mosquitto.org", "mqtt", server =>
    {
        server.Description = "Test Mosquitto MQTT Broker";
    });
    
    options.WithDefaultContentType("application/json")
        .WithInfo("Smartylighting Streetlights API", "1.0.0")
        .WithDescription("Manage city streetlights remotely.")
        .WithLicense("Apache 2.0", "https://www.apache.org/licenses/LICENSE-2.0");
});

var app = builder.Build();

// Map the AsyncAPI document endpoint (defaults to /asyncapi/v1.json)
app.MapAsyncApi();

// Optional: render the document with Scalar
app.MapScalarApiReference(options =>
{
    options.AddAsyncApiDocument("v1", "Streetlights API", "/asyncapi/v1.json");
});

app.Run();
```

## Defining your first Channel

Use attributes to define channels and operations on your message bus or hub classes.

```csharp
using Bielu.AspNetCore.AsyncApi.Attributes.Attributes;

[AsyncApi] // Tells the library to scan this class
public class StreetlightMessageBus
{
    private const string LightMeasuredTopic = "subscribe/light/measured";

    [Channel(LightMeasuredTopic, Servers = new[] { "mosquitto" })]
    [SubscribeOperation(typeof(LightMeasuredEvent), "Light", 
        Summary = "Subscribe to environmental lighting conditions for a particular streetlight.")]
    public void PublishLightMeasurement(LightMeasuredEvent lightMeasuredEvent)
    {
        // Logic to publish to your broker
    }
}
```

## Accessing the Documentation

Once your app is running:

- **JSON Document**: `GET /asyncapi/v1.json`
- **Scalar UI**: `GET /scalar` (if configured)

For more advanced scenarios, see the following guides:
- [Attributes](attributes.md)
- [Configuration](configuration.md)
- [Native AOT Support](native-aot.md)
- [Scalar Consoles](scalar-consoles.md)
