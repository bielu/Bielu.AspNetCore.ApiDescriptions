# Bielu.AspNetCore.ApiDescriptions.Mcp

**Experimental** (diagnostic `BIELUMCP001`). Lets AI agents read your app's own API descriptions through
a [Model Context Protocol](https://modelcontextprotocol.io/) server: every AsyncAPI, OpenAPI and Arazzo
document the app serves, plus read-only tools that answer questions like "which messages does this app
send, and what's in them?" without loading whole documents.

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
| `list_api_descriptions` | the documents, with their `apidescription://{kind}/{name}` resource URIs |
| `get_api_description` | one whole document |
| `list_asyncapi_operations` | what an AsyncAPI document says the app sends and receives, and on which channels |
| `get_asyncapi_message` | one message with the schemas it references resolved |

The documents are written as build-time generation writes them, overlays included, and are also
available as the resource template `apidescription://{kind}/{name}`.

## Documentation

- [MCP server for API descriptions](https://apidescriptions.bielu.pl/articles/mcp.html)
- [Full documentation](https://apidescriptions.bielu.pl/)

## Feedback & Contributing

Copyright (c) 2025 Arkadiusz Biel. Released under the [MIT license](https://licenses.nuget.org/MIT).
Bug reports and contributions are welcome at [the GitHub repository](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions).
