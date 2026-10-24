# MCP server for API descriptions

> [!WARNING]
> Experimental. The API reports the `BIELUMCP001` diagnostic, and tool names and outputs may change in a minor release.

`Bielu.AspNetCore.ApiDescriptions.Mcp` lets AI agents read an app's own API descriptions through a
[Model Context Protocol](https://modelcontextprotocol.io/) server. An agent connected to the app's MCP server can find
out what the app publishes and consumes, and with which payloads, from the same AsyncAPI, OpenAPI and Arazzo documents
the app serves. It doesn't need the documents' URLs or the whole document in its context.

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
app.MapMcp("/mcp");
```

The package is built on the official [C# SDK](https://github.com/modelcontextprotocol/csharp-sdk), `ModelContextProtocol`
2.2. `WithApiDescriptions()` adds tools and a resource to the server you configure. It works with any transport and
alongside your own tools.

## Tools

All tools are read-only: they never change the app or call its APIs, and they say so through MCP's `readOnlyHint`.

| Tool | Arguments | Returns |
| --- | --- | --- |
| `list_api_descriptions` | none | every document: `kind`, `name`, resource `uri`, `mimeType` |
| `get_api_description` | `kind`, `name` | the whole document, as JSON |
| `list_asyncapi_operations` | `document` | each operation of an AsyncAPI document: `id`, `action` (`send`/`receive`), `channel`, `address`, `messages`, `summary` |
| `get_asyncapi_message` | `document`, `message` | one message under `components/messages`, with every schema and component it references copied under `definitions` |

Kinds and names are matched without regard to case. A mistake, such as an unknown document or message, or a
document that fails to generate, is returned as a tool error that says what went wrong and what exists, so the agent can
correct itself.

AsyncAPI 2.x documents are summarized the same way: a channel's `subscribe` operation is one the app sends, and its
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
