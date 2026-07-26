# Arazzo overview

`Bielu.Arazzo.NET` and `Bielu.Arazzo.NET.Readers` are a framework-free object model, writers, reader,
and validator for the [Arazzo Specification](https://spec.openapis.org/arazzo/latest.html) — a
standard for describing multi-step API workflows. Arazzo 1.1 added `asyncapi` as a first-class
`sourceDescriptions` type alongside `openapi`, so a single workflow can span HTTP operations and
event/message channels.

> ⚠️ **Note:** Pre version 1.0.0, these libraries are regarded as unstable and **breaking changes may
> be introduced**. This page documents the spec library only; the ASP.NET Core integration
> (`Bielu.AspNetCore.Arazzo`) and `dotnet arazzo` CLI are planned but not yet built — see
> [ROADMAP.md](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/blob/main/ROADMAP.md).

## Installation

```bash
dotnet add package Bielu.Arazzo.NET
dotnet add package Bielu.Arazzo.NET.Readers
```

`Bielu.Arazzo.NET` has no dependency on YAML or on anything else in this repo — consumers that only
build and write documents (source generators, analyzers) never pull in a YAML parser. `YamlDotNet` is
confined to `Bielu.Arazzo.NET.Readers`.

## Reading a document

```csharp
using Bielu.Arazzo.Readers;

var result = ArazzoStringReader.Read(yamlOrJsonText);

if (result.Document is null)
{
    foreach (var error in result.Diagnostics.Errors)
    {
        Console.WriteLine($"{error.Path}: {error.Message}");
    }
    return;
}

var document = result.Document;
```

`ArazzoStringReader` auto-detects JSON vs. YAML from the input's first non-whitespace character.
`ArazzoStreamReader` and `ArazzoTextReader` cover the `Stream`/`TextReader` equivalents. Reading never
throws for malformed input — problems are reported as diagnostics on the returned
`ArazzoReadResult`.

## Validating a document

```csharp
using Bielu.Arazzo.Validation;

var errors = ArazzoValidator.Validate(document);
```

`ArazzoValidator` checks the structural invariants a well-formed document must satisfy beyond what the
type system already enforces — unique `workflowId`/`stepId` values, step target mutual-exclusivity,
JSON Schema shape checks on `workflow.inputs`, and more. It does **not** resolve references against
real source documents (does this `operationId` actually exist?) — that is `ArazzoWorkspace`'s job.

## Writing a document

```csharp
using Bielu.Arazzo.Writers;

var json = ArazzoJsonWriter.Write(document);
var yaml = ArazzoYamlWriter.Write(document);
```

Both writers serialize through the same `IArazzoWriter` tree-builder abstraction
(`ArazzoJsonNodeWriter`), and each model type implements `IArazzoSerializable.SerializeAsV1` — the
version-scoped serialization method ByteBard's AsyncAPI.NET uses the same pattern for.

## Runtime expressions

The `Bielu.Arazzo.Expressions` namespace implements the full §5.9 runtime-expression grammar —
`$url`, `$method`, `$statusCode`, `$request.*`, `$response.*`, `$message.*`, `$inputs.*`, `$outputs.*`,
`$steps.*`, `$workflows.*`, `$sourceDescriptions.*`, `$components.*`, and `$self`:

```csharp
using Bielu.Arazzo.Expressions;

if (RuntimeExpressionParser.TryParse("$message.payload#/status", out var expression, out var error))
{
    // expression is a RuntimeExpression.Message with a JSON Pointer into the payload
}
```

## Resolving workflow steps against live documents

`ArazzoWorkspace` is the seam that lets a step's `operationId`/`operationPath`/`channelPath` reference
be resolved against the actual OpenAPI/AsyncAPI/Arazzo documents a `sourceDescription` points at,
rather than only checked for well-formedness:

```csharp
using Bielu.Arazzo;

var workspace = new ArazzoWorkspace();
workspace.RegisterResolver(new MyAsyncApiSourceResolver());
workspace.RegisterDocument("events", "asyncapi", myAsyncApiDocument);

if (workspace.TryResolveOperation("events", "sendLightMeasurement", out var operation))
{
    // operation resolved against the live AsyncAPI document
}
```

Implement `IArazzoSourceResolver` per source type (`openapi`, `asyncapi`, `arazzo`). This is the hook
the planned `Bielu.AspNetCore.Arazzo` package plugs into so a running app can self-wire its own
`IAsyncApiDocumentProvider` and OpenAPI documents — turning a renamed channel or operation into a
startup failure instead of a production one.

## What's next

See the [Arazzo proposal](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/blob/main/ARAZZO-PROPOSAL.md)
and [roadmap](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/blob/main/ROADMAP.md) for the
ASP.NET Core builder/self-wiring package, the `dotnet arazzo` CLI, and the workflow runtime.
