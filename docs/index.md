# Bielu API Descriptions

Tools for describing, generating, and validating API specifications for ASP.NET Core — spanning both
request/response APIs and event-driven, workflow-based ones.

<div class="landing-cards">

<div class="landing-card">

## AsyncAPI

[Bielu.AspNetCore.AsyncApi](https://www.nuget.org/packages/Bielu.AspNetCore.AsyncApi/) generates
[AsyncAPI](https://www.asyncapi.com/) documents from minimal or controller-based APIs — the same
developer experience as `Microsoft.AspNetCore.OpenApi`, for event-driven channels and messages.

- Runtime and build-time document generation
- Document and schema transformers
- Protocol bindings — AMQP, HTTP, MQTT, Kafka, SignalR, gRPC, SSE, WebRTC
- Interactive UI via [Scalar](https://scalar.com/)
- CLI validation/diff and Roslyn analyzers

[Get started with AsyncAPI →](articles/getting-started.md)
&nbsp;·&nbsp;
[API reference →](api/asyncapi/index.md)

</div>

<div class="landing-card">

## Arazzo

[Bielu.Arazzo.NET](https://www.nuget.org/packages/Bielu.Arazzo.NET/) is the object model, writers,
validation, and reader for the [Arazzo Specification](https://spec.openapis.org/arazzo/latest.html) —
describing multi-step API workflows that can span both OpenAPI and AsyncAPI sources.

- `ArazzoDocument` model, JSON/YAML writers, structural validation
- Runtime-expression parser/evaluator for the full spec §5.9 grammar
- JSON and YAML readers with diagnostics (`Bielu.Arazzo.NET.Readers`)
- `ArazzoWorkspace` — the seam for resolving workflow steps against live OpenAPI/AsyncAPI documents

[Get started with Arazzo →](articles/arazzo/overview.md)
&nbsp;·&nbsp;
[API reference →](api/arazzo/index.md)

</div>

</div>

> ⚠️ **Note:** Pre version 1.0.0, these libraries are regarded as unstable and **breaking changes may be
> introduced**.
