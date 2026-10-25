---
"bielu-aspnetcore-asyncapi": minor
---

Add `WithApiCatalogItem()` to `Bielu.AspNetCore.ApiCatalog`: an endpoint convention that lists an API served from an endpoint rather than a document, such as an MCP server, as an `item` link in `/.well-known/api-catalog`. `app.MapMcp("/mcp").WithApiCatalogItem("MCP server")` lists the MCP endpoint once, however many methods it maps; routes with parameters are left out. The SignalR chat example now serves its MCP server and the API catalog together.
