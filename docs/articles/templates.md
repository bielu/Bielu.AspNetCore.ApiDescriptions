# Templates

Bielu.AspNetCore.AsyncApi provides a set of `dotnet new` templates to help you get started quickly with AsyncAPI in various project types.

## Installation

Install the template pack from NuGet:

```bash
dotnet new install Bielu.AspNetCore.AsyncApi.Templates
```

## Available Templates

### Web API (`asyncapi-webapi`)

Creates a minimal ASP.NET Core Web API project pre-configured with AsyncAPI and Scalar UI.

```bash
dotnet new asyncapi-webapi -n MyAsyncApiApp
```

### SignalR (`asyncapi-signalr`)

Creates a SignalR project with AsyncAPI documentation for the Hub and the interactive SignalR console enabled in Scalar.

```bash
dotnet new asyncapi-signalr -n MySignalRApp
```

### gRPC (`asyncapi-grpc`)

Creates a gRPC service project with AsyncAPI documentation and the interactive gRPC console enabled in Scalar.

```bash
dotnet new asyncapi-grpc -n MyGrpcApp
```

### Console Application (`asyncapi-console`)

Creates a Worker Service console application with AsyncAPI attributes for documenting background processing tasks.

```bash
dotnet new asyncapi-console -n MyWorkerApp
```

### Whole Solution (`asyncapi-sln`)

Creates a multi-project solution following best practices, including:
- **Contracts**: A shared library for messages and AsyncAPI attributes.
- **API**: A Web API project.
- **Worker**: A background service project.

```bash
dotnet new asyncapi-sln -n MySystem
```

## Usage

After creating a project from a template, you can run it immediately:

```bash
cd MyAsyncApiApp
dotnet run
```

The AsyncAPI documentation will be available at:
- JSON: `http://localhost:5000/asyncapi/v1.json`
- UI: `http://localhost:5000/scalar`

## Customization

You can customize the target framework using the `--Framework` parameter (default is `net10.0`):

```bash
dotnet new asyncapi-webapi -n MyApp --Framework net9.0
```
