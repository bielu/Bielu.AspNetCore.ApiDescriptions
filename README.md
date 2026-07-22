# Bielu.AspNetCore.AsyncApi

[![CI](https://github.com/bielu/Bielu.AspNetCore.AsyncApi/actions/workflows/buildAndPublishPackage.yml/badge.svg)](https://github.com/bielu/Bielu.AspNetCore.AsyncApi/actions/workflows/buildAndPublishPackage.yml)
[![NuGet](https://img.shields.io/nuget/v/Bielu.AspNetCore.AsyncApi.svg)](https://www.nuget.org/packages/Bielu.AspNetCore.AsyncApi/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Bielu.AspNetCore.AsyncApi.svg)](https://www.nuget.org/packages/Bielu.AspNetCore.AsyncApi/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

<p align="center">
  <img src="docs/images/logo.svg" alt="Bielu.AspNetCore.AsyncApi Logo" width="200">
</p>

Bielu.AspNetCore.AsyncApi provides built-in support for generating [AsyncAPI](https://www.asyncapi.com/) documents from minimal or controller-based APIs in ASP.NET Core. This library brings the same developer experience as [Microsoft.AspNetCore.OpenApi](https://www.nuget.org/packages/Microsoft.AspNetCore.OpenApi) but for AsyncAPI specifications.

📖 **Read the full documentation at: [https://asyncapi.bielu.pl/](https://asyncapi.bielu.pl/)**

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
- ✅ **API Versioning** integration for version-per-document support

## Documentation Sections

- [Getting Started](https://asyncapi.bielu.pl/articles/getting-started.html)
- [Attributes Reference](https://asyncapi.bielu.pl/articles/attributes.html)
- [Configuration Guide](https://asyncapi.bielu.pl/articles/configuration.html)
- [Scalar & Live Consoles](https://asyncapi.bielu.pl/articles/scalar-consoles.html)
- [CLI Tool usage](https://asyncapi.bielu.pl/articles/cli.html)
- [Migration from Saunter](https://asyncapi.bielu.pl/articles/migration-from-saunter.html)

## License

Licensed under the [MIT License](LICENSE).
