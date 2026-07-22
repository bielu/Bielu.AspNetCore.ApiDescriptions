# Bielu.AspNetCore.AsyncApi

Bielu.AspNetCore.AsyncApi provides built-in support for generating [AsyncAPI](https://www.asyncapi.com/) documents from minimal or controller-based APIs in ASP.NET Core. This library brings the same developer experience as [Microsoft.AspNetCore.OpenApi](https://www.nuget.org/packages/Microsoft.AspNetCore.OpenApi) but for AsyncAPI specifications.

> ⚠️ **Note:** Pre version 1.0.0, the API is regarded as unstable and **breaking changes may be introduced**.

## Key Features

- ✅ **Runtime document generation** - View generated AsyncAPI documents at runtime via a parameterized endpoint (`/asyncapi/{documentName}.json`)
- ✅ **Build-time document generation** - Generate AsyncAPI documents at build-time for static hosting
- ✅ **Document transformers** - Customize the generated document via document transformers
- ✅ **Schema transformers** - Customize the generated schema via schema transformers
- ✅ **Protocol bindings** - Support for AMQP, HTTP, MQTT, Kafka, SignalR, gRPC, SSE, WebRTC, and other protocol bindings
- ✅ **Multiple documents** - Generate multiple AsyncAPI documents from a single application
- ✅ **Interactive UI** - Render the document with [Scalar](https://scalar.com/)
- ✅ **Attribute-based configuration** - Decorate your classes with attributes to define channels and operations
- ✅ **XML Documentation** - Automatically populate descriptions from C# XML comments
- ✅ **Message Examples** - Embed examples directly in your message definitions
- ✅ **CLI Tool** - Validate and diff documents in your CI/CD pipelines
- ✅ **Roslyn Analyzers** - Catch attribute misuse at compile-time

## Quick Start

### 1. Installation

```bash
dotnet add package Bielu.AspNetCore.AsyncApi
```

### 2. Configure Services

```csharp
builder.Services.AddAsyncApi(options =>
{
    options.WithInfo("My API", "1.0.0");
});

var app = builder.Build();
app.MapAsyncApi();
app.Run();
```

### 3. Define Channels

```csharp
[AsyncApi]
public class MyService
{
    [Channel("my/topic")]
    [PublishOperation(typeof(MyMessage), "MyOperation")]
    public void SendMessage(MyMessage msg) { }
}
```

Learn more in the [Getting Started](articles/getting-started.md) guide.
