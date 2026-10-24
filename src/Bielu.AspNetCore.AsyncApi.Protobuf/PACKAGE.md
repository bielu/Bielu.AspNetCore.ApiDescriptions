# Bielu.AspNetCore.AsyncApi.Protobuf

Describes AsyncAPI payloads whose types are [Protocol Buffers](https://protobuf.dev/) messages, the
classes `Grpc.Tools` generates, by their `.proto` source, declared with
`schemaFormat: application/vnd.google.protobuf;version=3`.

The source is rendered from the generated message descriptor, so field numbers, types, labels, maps,
oneofs, nested types, enums, reserved ranges and proto2 defaults are the real wire contract, not a guess
from JSON Schema. Messages from editions files keep their JSON Schema.

## Installation

```sh
dotnet add package Bielu.AspNetCore.AsyncApi.Protobuf
```

## Usage

```csharp
builder.Services.AddAsyncApi(options => options
    .AddServer("grpc", "localhost:5001", "grpc")
    .AddProtobufPayloads());
```

Each Protocol Buffers payload becomes one self-contained `.proto` file. The payload message is marked
`// @RootNode`, as the AsyncAPI Protobuf parser expects, and declared together with every message and
enum it uses; `google/protobuf` and `google/type` stay imports. Other payloads keep their JSON Schema.
The output validates against the official AsyncAPI 2.6.0 and 3.1.0 schemas.

## Documentation

- [Protobuf payloads](https://apidescriptions.bielu.pl/articles/protobuf.html)
- [Full documentation](https://apidescriptions.bielu.pl/)

## Feedback & Contributing

Copyright (c) 2025 Arkadiusz Biel. Released under the [MIT license](https://licenses.nuget.org/MIT).
Bug reports and contributions are welcome at [the GitHub repository](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions).
