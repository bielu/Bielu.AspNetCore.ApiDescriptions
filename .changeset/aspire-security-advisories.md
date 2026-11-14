---
"bielu-aspnetcore-asyncapi": patch
---

Security: `Bielu.AspNetCore.AsyncApi.Scalar.SignalR.Aspire` and `Bielu.AspNetCore.AsyncApi.Scalar.Grpc.Aspire` now depend
on Scalar.Aspire 0.15.0, which brings Aspire.Hosting 13.6.5. Through Scalar.Aspire 0.11.12 they pulled in
Aspire.Hosting 13.2.1, affected by [GHSA-f7vh-h5g3-7jrh](https://github.com/advisories/GHSA-f7vh-h5g3-7jrh) (high).
The broker console's Kafka driver moves to Confluent.Kafka 2.15.0, the version Aspire.Confluent.Kafka 13.6.5 uses.
