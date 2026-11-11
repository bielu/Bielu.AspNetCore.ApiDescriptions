---
"bielu-aspnetcore-asyncapi": patch
---

Move the .NET 10 Microsoft package pins to the latest servicing release: `10.0.10` → `10.0.15`
(ASP.NET Core OpenAPI, ApiDescription.Server, test hosts, SignalR client, System.Text.Json,
Microsoft.Extensions.Options/DependencyInjection.Abstractions), and the `10.x` Microsoft.Extensions
libraries to `10.11.0`. `Microsoft.OpenApi` rises from `2.10.0` to `2.13.0`, the floor
`Microsoft.AspNetCore.OpenApi` 10.0.15 declares. The `asyncapi-console` and `asyncapi-sln`
templates now reference `Microsoft.Extensions.Hosting` 10.0.15.
