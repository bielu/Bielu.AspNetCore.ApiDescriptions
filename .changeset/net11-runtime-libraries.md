---
"bielu-aspnetcore-asyncapi": minor
---

Add .NET 11 support. Every runtime library package (AsyncAPI core and attributes, protocols, Scalar
consoles, Versioning, Wolverine, Avro, Protobuf, CloudEvents, XRegistry, Merger, ApiDescriptions,
ApiCatalog, MCP, Arazzo, Overlay and the shared helpers) now ships `lib/net10.0` and `lib/net11.0`
from the same source and public API. The `net11.0` assets use ASP.NET Core 11.0.0 packages
(`Microsoft.AspNetCore.OpenApi` 11.0.0, `Microsoft.OpenApi` 3.11.0); the `net10.0` assets keep the
10.0.x line, so .NET 10 consumers need no changes.

Fixed on .NET 11: Arazzo's startup validation again reports an `AddOpenApiSource` that names a
document never registered with `AddOpenApi`. ASP.NET Core 11 registers `IOpenApiDocumentProvider` for
any key, so the previous keyed-lookup check accepted every name.
