---
"bielu-aspnetcore-asyncapi": minor
---

Add `Bielu.AspNetCore.ApiDescriptions.Mcp`, a new experimental package (diagnostic `BIELUMCP001`) that lets AI agents read an app's own API descriptions through a Model Context Protocol server. `IMcpServerBuilder.WithApiDescriptions()` exposes every AsyncAPI, OpenAPI and Arazzo document the app serves as the resource template `apidescription://{kind}/{name}`, plus four read-only tools. `list_api_descriptions` and `get_api_description` list and return the documents, `list_asyncapi_operations` summarizes what an AsyncAPI document says the app sends and receives, and `get_asyncapi_message` returns one message with the schemas it references resolved. The documents are written as build-time generation writes them, overlays included. The package is built on the official `ModelContextProtocol` C# SDK 2.2.
