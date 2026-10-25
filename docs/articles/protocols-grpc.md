# gRPC Protocol

The `Bielu.AspNetCore.AsyncApi.Extensions.Protocols.Grpc` package adds support for documenting gRPC services.

## Installation

```bash
dotnet add package Bielu.AspNetCore.AsyncApi.Extensions.Protocols.Grpc
```

## Configuration

```csharp
builder.Services.AddAsyncApi(options =>
{
    options.AddGrpcChannelBinding("greeter", b =>
    {
        b.Service = "Greeter";
    });

    options.AddGrpcOperationBinding("sayHello", b =>
    {
        b.Method = "SayHello";
    });
});
```

## Bindings Reference

| Binding | Notable fields |
| --- | --- |
| `GrpcServerBinding` | `ProtocolVersion` |
| `GrpcChannelBinding` | `Service` |
| `GrpcOperationBinding` | `Method` |
| `GrpcMessageBinding` | `FieldNumbers` |

## Protobuf payloads

By default a gRPC message's payload is the JSON Schema of its generated class. To describe it with its `.proto`
source, the real wire contract with its field numbers, add `Bielu.AspNetCore.AsyncApi.Protobuf` and call
`options.AddProtobufPayloads()`. See [Protobuf payloads](protobuf.md).

## Interactive Console

To enable the live gRPC console in Scalar, see the [Scalar Consoles](scalar-consoles.md) guide.
