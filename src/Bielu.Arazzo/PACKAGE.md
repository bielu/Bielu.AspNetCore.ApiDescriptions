# Bielu.Arazzo.NET

Object model, writers and validation for the [Arazzo Specification](https://spec.openapis.org/arazzo/latest.html)
— the OpenAPI Initiative's format for describing **workflows** across API calls.

Arazzo 1.1.0 added AsyncAPI as a first-class `sourceDescriptions` type, which makes it the only
standard way to describe a workflow that spans HTTP requests *and* event/message channels.

This package is **framework-free** — no ASP.NET Core dependency, and no dependency on anything else in
the suite — so tooling, generators and applications can all consume it.

## Installation

```sh
dotnet add package Bielu.Arazzo.NET
```

## What's in it

- The full model: `ArazzoDocument`, `ArazzoWorkflow`, `ArazzoStep` (including the AsyncAPI-specific
  `ChannelPath`, `Action` and `CorrelationId` fields new in 1.1), `ArazzoCriterion`, and the rest.
- JSON and YAML writers over a shared `JsonNode` tree builder.
- A runtime-expression parser and evaluator for the full §5.9 grammar (`$inputs.*`, `$steps.*`,
  `$response.*`, `$message.*`, …).
- `ArazzoValidator` for the specification's structural invariants.

To read documents, add
[Bielu.Arazzo.NET.Readers](https://www.nuget.org/packages/Bielu.Arazzo.NET.Readers).

## Documentation

- [Arazzo overview](https://apidescriptions.bielu.pl/articles/arazzo/overview.html)
- [Full documentation](https://apidescriptions.bielu.pl/)

## Feedback & Contributing

Released under the [MIT license](https://licenses.nuget.org/MIT). Bug reports and contributions are
welcome at [the GitHub repository](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions).
