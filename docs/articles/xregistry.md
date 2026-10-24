# xRegistry

`Bielu.AspNetCore.AsyncApi.XRegistry` serves the messages and payload schemas of an app's AsyncAPI documents as a
read-only [CNCF xRegistry](https://xregistry.io/) (specification 1.0-rc4). xRegistry is the CloudEvents project's
standard API for message definition registries and schema registries. Registry-aware tooling can browse an app's
messages and fetch its schemas without knowing anything about AsyncAPI.

```sh
dotnet add package Bielu.AspNetCore.AsyncApi.XRegistry
```

```csharp
builder.Services.AddAsyncApi("orders");
builder.Services.AddXRegistry(options => options.WithRegistry("orders", "Orders registry"));

var app = builder.Build();
app.MapAsyncApi();
app.MapXRegistry(); // /registry and /.well-known/xregistry
```

`AddXRegistry()` is optional; without it every AsyncAPI document is served under the registry id `asyncapi`.

## What the registry contains

Each AsyncAPI document becomes a **message group** and a **schema group**, both with the document's name:

| AsyncAPI | xRegistry |
| --- | --- |
| a message under `components/messages` | a `message` in `messagegroups/{document}`, keyed by the message key |
| `title` (or `name`), `description` (or `summary`) | `name`, `description` |
| `contentType` (or `defaultContentType`) | `datacontenttype` |
| the message's payload | a `schema` in `schemagroups/{document}` with the same id, which the message points at with `dataschemaformat`, `dataschemauri` and `dataschemaxid` |

Payload schemas keep the format the AsyncAPI document declares:

| AsyncAPI `schemaFormat` | xRegistry `format` | Document |
| --- | --- | --- |
| none, or JSON Schema | `JsonSchema/draft-07` | a standalone JSON Schema: referenced component schemas are copied under `definitions`, and their `$ref`s point there |
| `application/vnd.apache.avro;version=X` | `Avro/X` | the Avro schema |
| `application/vnd.google.protobuf;version=X` | `Protobuf/X` | the `.proto` source |

The documents are read the way build-time generation writes them, so [overlays](overlay/overview.md) and other
serialized-document transformers are already applied. Combine with the [Avro](avro.md) and [Protobuf](protobuf.md)
packages to serve those formats.

The registry is generated from the app, so every entity has `epoch` 1, the time `MapXRegistry()` ran as `createdat`
and `modifiedat`, and exactly one Version, `1`.

## HTTP API

All of these are `GET` requests under the registry root (`/registry` by default):

| Path | Returns |
| --- | --- |
| `/` | the Registry entity |
| `/capabilities`, `/model` | what the registry supports, and its model |
| `/messagegroups`, `/messagegroups/{group}` | message groups |
| `/messagegroups/{group}/messages[/{message}]` | message definitions (metadata only; messages have no document) |
| `/schemagroups`, `/schemagroups/{group}` | schema groups |
| `/schemagroups/{group}/schemas` | schemas' metadata |
| `/schemagroups/{group}/schemas/{schema}` | the **schema document**, with its attributes as `xRegistry-` headers |
| `/schemagroups/{group}/schemas/{schema}$details` | the schema's metadata |
| `…/{resource}/meta`, `…/{resource}/versions[/1[$details]]` | the `meta` entity and Versions |
| `/.well-known/xregistry` | the host-based discovery document listing this registry |

The `?inline` flag is supported, with the specification's path rules:

```http
GET /registry?inline                                  # everything except model and capabilities
GET /registry?inline=model,capabilities
GET /registry?inline=messagegroups.messages           # messages, and the groups holding them
GET /registry/schemagroups/orders/schemas?inline=schema  # schema documents in the metadata
```

An unknown inline path gets the `bad_inline` error, and any other flag of the specification (`filter`, `sort`, `doc`,
…) gets `bad_flag`. A missing entity gets `not_found`. Errors are RFC 9457 problem documents.

## Options

| Option | Effect |
| --- | --- |
| `WithRegistry(id, name)` | The `registryid` and `name`. |
| `Description` | The registry's `description`. |
| `IncludeDocuments("v1", …)` | Only these AsyncAPI documents. |
| `MapXRegistry(prefix, mapWellKnownDiscovery)` | The registry root, and whether to serve `/.well-known/xregistry`. |

`MapXRegistry()` returns the endpoints' convention builder, so you can, for example, require authorization:
`app.MapXRegistry().RequireAuthorization()`.

## Not supported

The registry is read-only, so it has no write operations. Pagination, `filter`, `sort`, the document view
(`/export`, `?doc`), message `envelope`/`protocol` metadata and the `endpoints` group are not implemented.
