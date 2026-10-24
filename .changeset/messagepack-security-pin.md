---
"bielu-aspnetcore-asyncapi": patch
---

`Bielu.AspNetCore.AsyncApi.Scalar.SignalR.Aspire` and `Bielu.AspNetCore.AsyncApi.Scalar.Grpc.Aspire` now depend on MessagePack 2.5.307 directly. Before, they picked up 2.5.301 through `Scalar.Aspire` → `Aspire.Hosting` → `StreamJsonRpc`, which is affected by [GHSA-qhrr-8q5h-9q3h](https://github.com/advisories/GHSA-qhrr-8q5h-9q3h).
