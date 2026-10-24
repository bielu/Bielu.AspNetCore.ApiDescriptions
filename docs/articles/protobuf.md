# Protobuf payloads

`Bielu.AspNetCore.AsyncApi.Protobuf` describes payloads whose types are [Protocol Buffers](https://protobuf.dev/)
messages, the classes `Grpc.Tools` generates from `.proto` files, by their `.proto` source. AsyncAPI carries such a
payload as a string, declared with `schemaFormat: application/vnd.google.protobuf;version=3`.

```sh
dotnet add package Bielu.AspNetCore.AsyncApi.Protobuf
```

```csharp
builder.Services.AddAsyncApi(options => options
    .AddServer("grpc", "localhost:5001", "grpc")
    .AddProtobufPayloads());
```

```json
"payload": {
  "schemaFormat": "application/vnd.google.protobuf;version=3",
  "schema": "syntax = \"proto3\";\n\npackage greet;\n\n// @RootNode\nmessage HelloRequest {\n  string name = 1;\n}\n"
}
```

## Why the descriptor, not the JSON Schema

A Protocol Buffers message travels by field number, and the generated class's JSON Schema has no field numbers. The
source is rendered from the message's descriptor, which `Grpc.Tools` compiles into the generated code, so it is the
same contract the code serializes:

- field numbers, scalar types (`sint64`, `fixed32`, …), the `repeated`, `optional` and (proto2) `required` labels, and
  (proto2) `[default = …]` values;
- `map<K, V>` fields, `oneof`s, nested messages and enums;
- `reserved` field numbers and names;
- `proto2` or `proto3` syntax, with the matching `version=2` or `version=3`.

Payloads of other types keep their JSON Schema. JSON Schema can't be turned into a `.proto` safely, because the field
numbers would have to be invented.

Two kinds of Protocol Buffers message keep their JSON Schema too, since one file can't describe them faithfully:

- a message from an editions file. AsyncAPI's Protobuf schema format has only `version=2` and `version=3`, and its
  parser doesn't read editions;
- a message that uses a type from a file of another syntax, such as a proto3 message with a proto2 field type. Inlined
  into the payload's file, that type's labels and defaults would mean something else.

## The rendered file

AsyncAPI tools read one `.proto` string per payload. The [AsyncAPI Protobuf parser](https://github.com/asyncapi/protobuf-schema-parser)
treats the message no other message uses as the payload, or the one marked `@RootNode`. So the rendered source is a
single file:

- the payload message, marked with a `// @RootNode` comment;
- every message and enum it uses, transitively, declared in the same file under the payload's package. Types it does not
  use are left out;
- types from the well-known `google/protobuf/*` and `google/type/*` files as imports, referenced by their full names,
  since the parsers resolve those themselves.

A type from another package is declared under the payload's package, so its references resolve within the file. The
wire format doesn't change, since Protocol Buffers encodes fields by number, not by type name. If two used types from
different packages share a name, the first one found keeps it and the other is declared under its full name with
underscores for dots, so `test.inventory.Item` becomes `test_inventory_Item`. Each keeps its own fields.

## Options

| Option | Effect |
| --- | --- |
| `ForMessages("helloRequest", …)` / `MessageFilter` | Only these messages, by their key under `components/messages`. |

## How it works

A document names a payload's schema but not its .NET type, so `AddProtobufPayloads()` registers one transformer in two
roles. As a schema transformer, it marks the JSON Schema of every `IMessage` type with the message's full name; it reads
the descriptor from an instance, so it needs no reflection and works under Native AOT. As a document transformer, it
replaces each payload whose schema has the mark, then removes the marks. The JSON Schemas stay under
`components/schemas`.

The document transformer runs where it is added among the document transformers, so add it after anything else that
adds messages.

## Conformance

The test suite validates the output against the official AsyncAPI 2.6.0 and 3.1.0 JSON Schemas.
