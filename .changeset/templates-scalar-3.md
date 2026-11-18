---
"bielu-aspnetcore-asyncapi": patch
---

The `dotnet new` templates reference `Scalar.AspNetCore` 3.0.1 instead of 2.16.17. In 3.0.0 the Scalar console packages need `Scalar.AspNetCore` 3.0.1 or later, so a project generated from `asyncapi-signalr`, `asyncapi-grpc` or `asyncapi-sln` failed to restore with a package downgrade error (NU1605).
