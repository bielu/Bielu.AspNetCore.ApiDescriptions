# Bielu.AspNetCore.AsyncApi.Templates

`dotnet new` templates for AsyncAPI-documented .NET projects.

## Installation

```sh
dotnet new install Bielu.AspNetCore.AsyncApi.Templates
```

## Templates

| Template | What you get |
| --- | --- |
| `asyncapi-webapi` | Minimal API with an `[AsyncApi]` message bus, `MapAsyncApi()` and Scalar |
| `asyncapi-signalr` | SignalR hub with bindings and the interactive SignalR console |
| `asyncapi-grpc` | gRPC service over gRPC-Web with the interactive gRPC console |
| `asyncapi-console` | Worker service with `[AsyncApi]` annotations and XML doc comments |
| `asyncapi-sln` | Multi-project solution: Contracts, Api and Worker |

```sh
dotnet new asyncapi-webapi -o MyApi
```

## Documentation

- [Templates](https://apidescriptions.bielu.pl/articles/templates.html)
- [Full documentation](https://apidescriptions.bielu.pl/)

## Feedback & Contributing

Released under the [MIT license](https://licenses.nuget.org/MIT). Bug reports and contributions are
welcome at [the GitHub repository](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions).
