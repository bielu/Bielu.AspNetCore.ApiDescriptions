# Bielu.AspNetCore.AsyncApi

[![CI](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/actions/workflows/buildAndPublishPackage.yml/badge.svg)](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/actions/workflows/buildAndPublishPackage.yml)
[![NuGet](https://img.shields.io/nuget/v/Bielu.AspNetCore.AsyncApi.svg)](https://www.nuget.org/packages/Bielu.AspNetCore.AsyncApi/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Bielu.AspNetCore.AsyncApi.svg)](https://www.nuget.org/packages/Bielu.AspNetCore.AsyncApi/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

<p align="center">
  <img src="docs/images/logo.svg" alt="Bielu.AspNetCore.AsyncApi Logo" width="200">
</p>

Bielu.AspNetCore.AsyncApi provides built-in support for generating [AsyncAPI](https://www.asyncapi.com/) documents from minimal or controller-based APIs in ASP.NET Core. This library brings the same developer experience as [Microsoft.AspNetCore.OpenApi](https://www.nuget.org/packages/Microsoft.AspNetCore.OpenApi) but for AsyncAPI specifications.

📖 **Read the full documentation at: [https://apidescriptions.bielu.pl/](https://apidescriptions.bielu.pl/)**

## Quick Start

### 1. Installation

```bash
dotnet add package Bielu.AspNetCore.AsyncApi
```

### 2. Configure Services

```csharp
using Bielu.AspNetCore.AsyncApi.Extensions;

var builder = WebApplication.CreateBuilder(args);

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
using Bielu.AspNetCore.AsyncApi.Attributes.Attributes;

[AsyncApi]
public class MyService
{
    [Channel("my/topic")]
    [PublishOperation(typeof(MyMessage), "MyOperation")]
    public void SendMessage(MyMessage msg) { }
}
```

## Key Features

- ✅ **Runtime & Build-time document generation**
- ✅ **Protocol bindings** (AMQP, HTTP, MQTT, Kafka, SignalR, gRPC, SSE, WebRTC)
- ✅ **Interactive Scalar UI** with live protocol consoles
- ✅ **Multiple documents** support
- ✅ **XML Documentation** support
- ✅ **CLI Tool** for validation and diffing
- ✅ **Roslyn Analyzers** for compile-time safety
- ✅ **Native AOT support**
- ✅ **API Versioning** integration for version-per-document support
- ✅ **OpenAPI Overlays** applied in the generation pipeline, so the served document is already transformed

## Documentation Sections

- [Getting Started](https://apidescriptions.bielu.pl/articles/getting-started.html)
- [Attributes Reference](https://apidescriptions.bielu.pl/articles/attributes.html)
- [Configuration Guide](https://apidescriptions.bielu.pl/articles/configuration.html)
- [Scalar & Live Consoles](https://apidescriptions.bielu.pl/articles/scalar-consoles.html)
- [Native AOT Support](https://apidescriptions.bielu.pl/articles/native-aot.html)
- [CLI Tool usage](https://apidescriptions.bielu.pl/articles/cli.html)
- [Migration from Saunter](https://apidescriptions.bielu.pl/articles/migration-from-saunter.html)
- [Arazzo Overview](https://apidescriptions.bielu.pl/articles/arazzo/overview.html)
- [Arazzo CLI Tool usage](https://apidescriptions.bielu.pl/articles/arazzo/cli.html)
- [Overlay Overview](https://apidescriptions.bielu.pl/articles/overlay/overview.html)
- [Overlays in the generation pipeline](https://apidescriptions.bielu.pl/articles/overlay/pipeline-integration.html)
- [Overlay CLI Tool usage](https://apidescriptions.bielu.pl/articles/overlay/cli.html)

## License

Licensed under the [MIT License](LICENSE).
