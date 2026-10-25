# Bielu.AspNetCore.ApiDescriptions.Mcp

**Experimental** (diagnostic `BIELUMCP001`). Lets AI agents read your app's own API descriptions through
a [Model Context Protocol](https://modelcontextprotocol.io/) server, so an agent can build a working
integration from the app's published contracts: find the operation it needs, then get its complete
documented contract (servers, security, bindings, messages and schemas) without loading whole documents.

## Installation

```sh
dotnet add package Bielu.AspNetCore.ApiDescriptions.Mcp
dotnet add package ModelContextProtocol.AspNetCore
```

## Usage

```csharp
#pragma warning disable BIELUMCP001
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithApiDescriptions();
#pragma warning restore BIELUMCP001

app.MapMcp("/mcp");
```

| Tool | Returns |
| --- | --- |
| `list_api_descriptions` | the documents: URIs, titles, specification versions, and which tools can inspect each |
| `find_api_operations` | the operations matching a query, across documents, each with a handle |
| `get_api_operation` | one operation's complete documented contract, its references keyed by canonical pointer, and what the document does not specify |
| `get_api_description` | one whole document |
| `list_asyncapi_operations` | what an AsyncAPI document says the app sends and receives, and on which channels |
| `get_asyncapi_message` | one message with every part of the document it references |

Operation search and inspection cover AsyncAPI documents so far.

The documents are written as build-time generation writes them, overlays included, and are also
available as the resource template `apidescription://{kind}/{name}`.

## Documentation

- [MCP server for API descriptions](https://apidescriptions.bielu.pl/articles/mcp.html)
- [Full documentation](https://apidescriptions.bielu.pl/)

## Feedback & Contributing

Copyright (c) 2025 Arkadiusz Biel. Released under the [MIT license](https://licenses.nuget.org/MIT).
Bug reports and contributions are welcome at [the GitHub repository](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions).
