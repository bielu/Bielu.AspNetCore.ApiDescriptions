---
"bielu-aspnetcore-asyncapi": minor
---

add an interactive gRPC console for Scalar: new `Bielu.AspNetCore.AsyncApi.Scalar.Grpc` (ASP.NET Core) and `Bielu.AspNetCore.AsyncApi.Scalar.Grpc.Aspire` (Aspire hosting) packages, powered by the new `@bielu/scalar-grpc` npm bundle on the shared `Bielu.AspNetCore.AsyncApi.Scalar` / `@bielu/scalar-core` foundation. `MapScalarGrpcAssets()` serves the console bundle plus a protobuf descriptor endpoint (`{assetsPath}/descriptors`, a serialized `FileDescriptorSet` gathered from the mapped gRPC services) and `options.WithGrpcClient(...)` injects the console into Scalar. The console parses `grpc` AsyncAPI bindings, prefills JSON requests from payload schemas and invokes unary + server-streaming methods over gRPC-Web with Scalar auth passed through as call metadata; client-/bidi-streaming methods are documentation-only. Wired into the `GrpcGreeter` example (`UseGrpcWeb(DefaultEnabled = true)`).
