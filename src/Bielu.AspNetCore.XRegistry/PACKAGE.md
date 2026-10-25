# Bielu.AspNetCore.XRegistry

Serves the messages and payload schemas of your app's AsyncAPI documents as a read-only
[CNCF xRegistry](https://xregistry.io/) (1.0-rc4): a message definitions registry and a schema registry
with the standard HTTP API, so registry-aware tooling can discover them.

## Installation

```sh
dotnet add package Bielu.AspNetCore.XRegistry
```

## Usage

```csharp
builder.Services.AddAsyncApi("orders");
builder.Services.AddXRegistry(options => options.WithRegistry("orders", "Orders registry"));

var app = builder.Build();
app.MapAsyncApi();
app.MapXRegistry(); // GET /registry, /.well-known/xregistry
```

Each AsyncAPI document becomes a message group and a schema group. Every message is a message
definition pointing at its payload schema, which is served as JSON Schema (draft-07, standalone), Avro
or Protobuf, as the document declares it:

```http
GET /registry/messagegroups/orders/messages/orderPlaced          # message definition
GET /registry/schemagroups/orders/schemas/orderPlaced            # the schema document
GET /registry/schemagroups/orders/schemas/orderPlaced$details    # its metadata
GET /registry?inline                                             # everything in one response
```

## Documentation

- [xRegistry](https://apidescriptions.bielu.pl/articles/xregistry.html)
- [Full documentation](https://apidescriptions.bielu.pl/)

## Feedback & Contributing

Copyright (c) 2025 Arkadiusz Biel. Released under the [MIT license](https://licenses.nuget.org/MIT).
Bug reports and contributions are welcome at [the GitHub repository](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions).
