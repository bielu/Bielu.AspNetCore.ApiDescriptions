# Arazzo overview

`Bielu.Arazzo.NET` and `Bielu.Arazzo.NET.Readers` provide a framework-free object model, writers, reader,
and validator for the [Arazzo Specification](https://spec.openapis.org/arazzo/latest.html) — a
standard for describing multi-step API workflows. Arazzo 1.1 added `asyncapi` as a first-class
`sourceDescriptions` type alongside `openapi`, so a single workflow can span HTTP operations and
event/message channels.

> ⚠️ **Note:** Before version 1.0.0, these libraries — and `Bielu.AspNetCore.Arazzo`, the ASP.NET Core
> integration covered below — are regarded as unstable and **breaking changes may be introduced**.

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
(`ArazzoJsonNodeWriter`), and each model type implements `IArazzoSerializable.SerializeAsV1` — a
version-scoped serialization method, following the same pattern ByteBard's AsyncAPI.NET uses for its
own model types.

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
`Bielu.AspNetCore.Arazzo` plugs into so a running app can self-wire its own `IAsyncApiDocumentProvider`
and OpenAPI documents — turning a renamed channel or operation into a startup failure instead of a
production one.

## ASP.NET Core integration: `Bielu.AspNetCore.Arazzo`

`Bielu.AspNetCore.Arazzo` mirrors the core AsyncAPI package's shape: a fluent options builder,
`AddArazzo`/`MapArazzo`, and — the differentiating feature — self-wiring `sourceDescriptions` against
the *same app's* live AsyncAPI/OpenAPI documents.

```bash
dotnet add package Bielu.AspNetCore.Arazzo
```

```csharp
using Bielu.Arazzo.Models;
using Bielu.AspNetCore.Arazzo.Extensions;

builder.Services.AddOpenApi("v1");
builder.Services.AddAsyncApi("v1");

builder.Services.AddArazzo(options =>
{
    options.WithInfo("Streetlights workflows", "1.0.0");
    options.AddAsyncApiSource("events", "v1");   // self-wires against the app's own AsyncAPI document
    options.AddOpenApiSource("api", "v1");       // self-wires against the app's own OpenAPI document

    options.AddWorkflow("measureAndAlert", wf => wf
        .Step("publishMeasurement", s => s
            .Channel("events", "lightMeasured", ArazzoStepAction.Send)
            .Output("measurementId", "$message.payload#/id"))
        .Step("awaitAlert", s => s
            .DependsOn("publishMeasurement")
            .Channel("events", "lightingAlert", ArazzoStepAction.Receive)
            .SuccessCriteria("$message.payload#/measurementId == $steps.publishMeasurement.outputs.measurementId")));
});

var app = builder.Build();
app.MapAsyncApi();
app.MapOpenApi();
app.MapArazzo();   // → /arazzo/{documentName}.json (default route; JSON only)
app.Run();
```

By default, `MapArazzo()` serves only JSON at `/arazzo/{documentName}.json`. To also serve YAML, map a
second route with a `.yaml`/`.yml` pattern:

```csharp
app.MapArazzo("/arazzo/{documentName}.yaml");
```

By default (`ArazzoOptions.ValidateSourceReferencesOnStartup = true`), every step's
`operationId`/`operationPath`/`channelPath` is resolved against the live, in-memory AsyncAPI/OpenAPI
documents once at app startup — a renamed channel or operation throws `ArazzoStartupValidationException`
and fails startup, rather than failing the first time a workflow actually runs in production.

### Identifying workflows and steps by type

Workflow and step ids are cross-referenced by string (`dependsOn`, a step targeting another workflow),
which makes a typo a runtime problem rather than a compile-time one. Every id-taking builder method has
a generic overload that takes a marker type instead, so renaming the type moves every reference with it:

```csharp
// Marker types — they carry no members; the type itself is the identifier.
sealed class MeasureAndAlert;
sealed class PublishMeasurement;
sealed class AwaitAlert;

options.AddWorkflow<MeasureAndAlert>(wf => wf
    .Step<PublishMeasurement>(s => s
        .Channel("events", "lightMeasured", ArazzoStepAction.Send)
        .Output("measurementId", "$message.payload#/id"))
    .Step<AwaitAlert>(s => s
        .DependsOn<PublishMeasurement>()
        .Channel("events", "lightingAlert", ArazzoStepAction.Receive)));

options.AddWorkflow<ReportDaily>(wf => wf
    .DependsOn<MeasureAndAlert>()                       // workflow-level dependsOn
    .Step<Summarise>(s => s.Workflow<MeasureAndAlert>()) // a step targeting another workflow
);
```

The mapping is `ArazzoId.FromType<T>()`: the type name camel-cased, so `MeasureAndAlert` becomes
`measureAndAlert` (and `HTTPHealthCheck` becomes `httpHealthCheck`). That keeps the emitted document's
casing idiomatic while the marker types stay idiomatic C#, and it means the two forms interoperate —
`AddWorkflow("measureAndAlert", …)` and `DependsOn<MeasureAndAlert>()` refer to the same workflow, so you
can adopt the generic form incrementally.

> ⚠️ **Security note:** `Bielu.AspNetCore.Arazzo` only *serves* and *validates* workflow documents — it
> does not execute them. Any future execution engine is intended to be CLI/test-only by design, and would
> never be exposed as a default ASP.NET Core endpoint: a hosted endpoint that ran arbitrary workflow steps
> would let a document drive outbound requests from inside your application.

## CLI Tool: `dotnet arazzo`

`Bielu.Arazzo.Cli` provides `validate`, `lint`, and `diff` commands for Arazzo documents — see the
[CLI Tool](cli.md) article for details.

## What's next

A workflow runtime — executing steps, evaluating criteria, and propagating outputs — is the natural next
piece, exposed through the CLI as a test/automation runner rather than as a hosted endpoint.
