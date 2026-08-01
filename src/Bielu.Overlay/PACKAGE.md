# Bielu.Overlay.NET

Object model, validation and apply engine for the
[OpenAPI Overlay Specification](https://spec.openapis.org/overlay/latest.html) — 1.0.0 and 1.1.0.

An overlay is a small document of ordered actions, each selecting nodes with an RFC 9535 JSONPath
`target` and then `update`-ing, `copy`-ing or `remove`-ing them. It turns "strip the internal
endpoints before publishing" from imperative code into a reviewable artifact.

## Not just OpenAPI

The engine operates on `System.Text.Json.Nodes.JsonNode` **and nothing else**. It has no dependency
on any object model, so it applies equally to OpenAPI, AsyncAPI, Arazzo, or any other JSON/YAML
description. That is the point: every other implementation is bound to a single document type.

## Installation

```sh
dotnet add package Bielu.Overlay.NET
```

## Usage

```csharp
var result = OverlayApplier.Apply(document, overlay, new OverlayApplyOptions { Strict = true });

JsonNode? transformed = result.Document;
```

Actions apply sequentially against the mutated tree, as the specification requires, and the
merge/copy/remove semantics are gated on the version the overlay declares — 1.1.0 added `copy`,
primitive targets and array concatenation, and a `1.0.0` document still gets 1.0.0 behaviour.

Verified against the OpenAPI Initiative's own conformance suite. To read overlay files, add
[Bielu.Overlay.NET.Readers](https://www.nuget.org/packages/Bielu.Overlay.NET.Readers).

## Documentation

- [Overlay overview](https://apidescriptions.bielu.pl/articles/overlay/overview.html)
- [Full documentation](https://apidescriptions.bielu.pl/)

## Feedback & Contributing

Released under the [MIT license](https://licenses.nuget.org/MIT). Bug reports and contributions are
welcome at [the GitHub repository](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions).
