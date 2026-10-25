# MCP server for API descriptions

> [!WARNING]
> Experimental. The API reports the `BIELUMCP001` diagnostic, and tool names and outputs may change in a minor release.

`Bielu.AspNetCore.ApiDescriptions.Mcp` lets AI agents read an app's own API descriptions through a
[Model Context Protocol](https://modelcontextprotocol.io/) server. The job it is built for: an agent connects to the app
and builds a working integration from its published contracts. It finds the operation it needs, then gets that
operation's complete documented contract (where to connect, how to authenticate, what to call, what to send and what
comes back) without the documents' URLs or a whole document in its context.

```sh
dotnet add package Bielu.AspNetCore.ApiDescriptions.Mcp
dotnet add package ModelContextProtocol.AspNetCore   # for the HTTP transport
```

```csharp
builder.Services.AddAsyncApi();
builder.Services.AddOpenApi();

#pragma warning disable BIELUMCP001
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithApiDescriptions();
#pragma warning restore BIELUMCP001

var app = builder.Build();
app.MapMcp("/mcp").WithApiCatalogItem("MCP server"); // optional: list it in the API catalog
app.MapApiCatalog();
```

`WithApiCatalogItem()` comes from [`Bielu.AspNetCore.ApiCatalog`](api-catalog/overview.md): it lists the MCP endpoint in
the app's `/.well-known/api-catalog` next to the documents, so an agent that knows only the app's address can find it.

The package is built on the official [C# SDK](https://github.com/modelcontextprotocol/csharp-sdk), `ModelContextProtocol`
2.3. `WithApiDescriptions()` adds tools and a resource to the server you configure. It works with any transport and
alongside your own tools. The [SignalR chat example](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/tree/main/src/examples/SignalRChat)
serves it at `/mcp`.

## Tools

All tools are read-only: they never change the app or call its APIs, and they say so through MCP's `readOnlyHint`.

| Tool | Arguments | Returns |
| --- | --- | --- |
| `list_api_descriptions` | none | every document: `kind`, `name`, resource `uri`, `mimeType`, `title`, `description`, `specificationVersion`, `fingerprint`, and `capabilities` (the tools that can inspect it) |
| `find_api_operations` | `query`, and optionally `kind`, `document`, `action`, `protocol`, `limit`, `cursor` | the best-matching operations across documents, each with a `handle` for `get_api_operation` |
| `get_api_operation` | a handle's `kind`, `document`, `operation`; optionally `cursor` | the operation's complete documented contract (see below) |
| `get_api_description` | `kind`, `name` | the whole document, as JSON |
| `list_asyncapi_operations` | `document` | each operation of an AsyncAPI document: `id`, `action`, `channel`, `address`, `messages` (each `name` and canonical `ref`), `summary` |
| `get_asyncapi_message` | `document`, `message` | one message, with every part of the document it references under `references` |

The usual path is `find_api_operations` → `get_api_operation`. `get_api_description` and the resource remain the
fallback for anything the focused tools do not cover. Only AsyncAPI documents can be searched and inspected so far:
`list_api_descriptions` says which tools work on each document, and `find_api_operations` lists the documents it did
not search, and why.

`list_api_descriptions`, `find_api_operations` and `get_api_operation` declare an `outputSchema` and return
`structuredContent`, with the same JSON as text for clients that only read text.

Kinds and names are matched without regard to case. A mistake, such as an unknown document or message, or a
document that fails to generate, is returned as a tool error that says what went wrong and what exists, so the agent can
correct itself.

### Finding operations

`find_api_operations` matches words of the query against each operation's id, title, summary and description, tags,
channel name and address, message names and titles, and its protocol binding values, such as a SignalR hub method name.
Identifiers are split at camel case (`SendMessage` matches "send message"), and a plural `s` is ignored. Operations
matching more words come first. Without a query it lists every operation that passes the filters. `protocol` matches
the protocol of the operation's servers and the bindings of the operation and its channel. Results are paged with
`limit` (at most 50) and `nextCursor`.

### An operation's contract

`get_api_operation` returns:

- `operation`: the operation as written, its `$ref`s kept;
- `channel`, `servers` (protocol, host or URL) and `security` (each scheme, its type, and whether it comes from the
  operation or a server);
- `messages`: each message's name, canonical `pointer`, content type, and `payload` and `headers` schemas as written;
- `examples`: pointers to the messages' authored examples. None are generated;
- `references`: every part of the document these refer to, transitively, keyed by canonical JSON Pointer, so each
  `$ref` can be looked up as written. A cycle is included once;
- `notSpecified`: what the document does not say, such as a missing reply, security, bindings, servers or channel
  parameters. These are gaps in the contract, not in the response;
- `omitted`: what was left out because it does not apply, such as the channel's messages this operation does not use;
- `unresolved`: references that could not be followed, such as those into other documents;
- `document.fingerprint`: identifies the exact document content the response quotes.

When `references` exceed about 48 KB, `nextCursor` is returned. Call again with it for the next page. A cursor stops
working once the document changes.

`action` is always relative to the documented app, as in AsyncAPI: `send` means the app sends the messages, so a
client of the app would receive them. The tools do not infer a client's configuration beyond what the bindings say.
Broker routing, credentials or deployment addresses the document does not give stay explicit gaps.

### Message identity

A message is known by its canonical pointer: where the message itself is, once every `$ref` is followed. For an
AsyncAPI 3 operation that refers to `#/channels/orders/messages/created`, which refers to
`#/components/messages/OrderCreated`, that is `#/components/messages/OrderCreated`. A message written inline in a
channel is known by its place in the channel. `get_asyncapi_message` accepts the pointer, the component key, or the name
an operation or channel uses. When a name is ambiguous it says so and lists the pointers to choose from.

AsyncAPI 2.x documents are inspected the same way: a channel's `subscribe` operation is one the app sends, and its
`publish` operation one the app receives.

## Resource

Every document is also the resource `apidescription://{kind}/{name}`, for example `apidescription://asyncapi/v1`, served
with its media type (`application/vnd.aai.asyncapi+json`, `application/vnd.oai.openapi+json`,
`application/vnd.oai.workflows+json`). The server lists it as a resource template, since the documents are only known
once the app runs; `list_api_descriptions` returns the concrete URIs.

## Which documents

- the AsyncAPI documents of `AddAsyncApi`;
- the Arazzo documents of `AddArazzo`;
- the OpenAPI documents of `Microsoft.AspNetCore.OpenApi`'s `AddOpenApi`;
- the documents of any other `IBuildTimeDocumentSource`.

They are written the way build-time generation writes them, so [overlays](overlay/overview.md) and other
serialized-document transformers have been applied. An overlay that removes internal channels from the published
document removes them from what agents see too.

## Security

The tools expose exactly what the documents expose. To limit who can use them, secure the MCP endpoint as you would any
other endpoint, for example `app.MapMcp("/mcp").RequireAuthorization()`.

## Status

The package stays experimental until the focused tools show they help. The test is to give an agent the same task
against the SignalR chat example in three set-ups: the whole AsyncAPI document fetched directly, whole-document access
through MCP, and `find_api_operations` with `get_api_operation`. The task is to write a minimal .NET client that
registers the receive handler, connects, sends a message and shows it arrives, without the example's source. Compare
completion without correction, wrong method or argument guesses, retrieval calls, tokens retrieved and time taken, over
repeated runs, and again with a larger document of unrelated operations. The repository's own test of that workflow
(`SignalRChatAgentWorkflowTests`) builds the client from the tool results alone, so it shows that the contract is
enough. It does not show that an agent does better with it.

Validating payloads against message schemas, and inspecting OpenAPI operations and Arazzo workflows, are planned once
that result is in.
