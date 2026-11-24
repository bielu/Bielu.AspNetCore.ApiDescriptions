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
JSON Schema shape checks on `workflow.inputs`, and more. It also checks that every
`$components.{kind}.{name}` reference and `#/components/inputs/{name}` `$ref` resolves to an entry of
the right kind, that outputs are expressions or Selector Objects rather than literals, and that a
document declaring `arazzo: 1.0.x` uses none of the 1.1 additions: `$self`, the Selector Object,
`targetSelectorType`, `asyncapi` source descriptions, the `channelPath`, `action`, `correlationId`,
`timeout` and `dependsOn` step fields, the `querystring` parameter location, and the `jsonpointer`
criterion type and `rfc9535` JSONPath version. It does **not** resolve references against real source documents
(does this `operationId` actually exist?). That is `ArazzoWorkspace`'s job.

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

Only steps whose source is self-wired (`AddOpenApiSource`/`AddAsyncApiSource`/`AddSelfWiredSource`) are
checked. A source added by URL alone with `AddSourceDescription`, such as another service's OpenAPI document,
isn't fetched at startup, so steps that target it are not checked; a step naming a source the document doesn't
declare at all still fails. A qualified `operationId` (`$sourceDescriptions.<name>.<operationId>`) is checked
against the source it names, the same way.

#### Self-wiring other documents

`AddOpenApiSource` and `AddAsyncApiSource` get their documents from an `IArazzoSourceDocumentProvider`, one of
the two `AddArazzo` registers for `openapi` and `asyncapi`. Register your own provider to validate steps
against a document the app doesn't serve itself, such as a partner's OpenAPI description shipped with the app,
and add the source with `AddSelfWiredSource`:

```csharp
using Bielu.AspNetCore.Arazzo.SourceResolvers;

builder.Services.TryAddEnumerable(
    ServiceDescriptor.Singleton<IArazzoSourceDocumentProvider, PartnerOpenApiProvider>());
builder.Services.AddArazzo(options => options
    .AddOpenApiSource("orders", "v1")
    .AddSelfWiredSource("payments", ArazzoSourceDescriptionType.OpenApi, "payments",
        "https://payments.example.com/openapi.json"));

sealed class PartnerOpenApiProvider : IArazzoSourceDocumentProvider
{
    public string SourceType => ArazzoSourceDescriptionType.OpenApi.Value;

    public async ValueTask<object?> GetDocumentAsync(string documentName, CancellationToken cancellationToken)
    {
        if (documentName != "payments")
        {
            return null; // not ours: the next provider for "openapi" is asked
        }

        await using var stream = File.OpenRead("payments.openapi.json");
        var result = await OpenApiDocument.LoadAsync(stream, "json", null, cancellationToken);
        return result.Document;
    }
}
```

Providers for the same type are asked newest first, and the first that returns a document wins, so a provider
registered after `AddArazzo` can own some document names and leave the rest to the built-in one. The document is
read by the `IArazzoSourceResolver` registered for the same type, so it must be the type that resolver reads:
an `OpenApiDocument` for `openapi`, an `AsyncApiDocument` for `asyncapi`. When no provider returns a document,
startup fails with an error naming the source and the document name.

The documents can also be written at build time with `dotnet getdocument`, next to the OpenAPI and AsyncAPI ones; see
[Build-time document generation](../build-time-generation.md#arazzo-documents). The tool starts the app, so the same
validation fails the build when a step references something that no longer exists.

### Authoring the rest of the document

Beyond targets, criteria and expression outputs, the builders cover the Arazzo fields that take
values, reusable objects and extensions. Values that can be a literal, a runtime expression or (Arazzo
1.1) a Selector Object use the model's own `ArazzoValue` and `ArazzoSelector` types:

```csharp
builder.Services.AddArazzo(options =>
{
    options.WithInfo("Order workflows", "1.0.0")
        .WithSelf("https://example.com/arazzo/orders.json")          // $self (1.1)
        .WithExtension("x-owner", "orders-team")                       // root x-* extension
        .AddOpenApiSource("orders", "v1")
        .AddComponentParameter("tenant", new ArazzoParameter
        {
            Name = "X-Tenant",
            In = ArazzoParameterLocation.Header,
            Value = "$inputs.tenant",
        })
        .AddComponentFailureAction("giveUp", new ArazzoFailureAction { Name = "giveUp", Type = "end" });

    options.AddWorkflow("placeOrder", wf => wf
        .ParameterReference("tenant")                                  // applies to every step
        .OnFailureReference("giveUp")
        .Step("create", s => s
            .Operation("createOrder")
            // A payload template, then the values written into it before the request is sent.
            .Payload(new JsonObject { ["sku"] = null, ["quantity"] = 1 }, "application/json")
            .Replacement("/sku", "$inputs.sku")
            .Replacement("$.quantity", ArazzoValue.FromSelector(new ArazzoSelector
            {
                Context = "$inputs.cart",
                Selector = "$.lines[0].quantity",
                Type = "jsonpath",
            }), "jsonpath")
            .Output("orderId", new ArazzoSelector { Context = "$response.body", Selector = "/id", Type = "jsonpointer" }))
        .Output("orderId", "$steps.create.outputs.orderId"));        // read as $workflows.placeOrder.outputs.orderId

    options.AddWorkflow("trackOrder", wf => wf
        .DependsOn("placeOrder")
        .Step("status", s => s
            .Operation("getOrder")
            .Parameter(new ArazzoParameter
            {
                Name = "id",
                In = ArazzoParameterLocation.Path,
                Value = "$workflows.placeOrder.outputs.orderId",
            })));
});
```

- **Request bodies.** `Payload`, `PayloadExpression` and `PayloadSelector` set the payload;
  `Replacement(target, value, targetSelectorType)` adds Payload Replacement Objects. Replacements
  are kept in the order they were added, whether they come before or after the payload, and setting
  the payload again replaces only the payload and its content type.
- **Values.** `Parameter(ArazzoParameter)` takes a literal (`ArazzoValue.FromLiteral`), an
  expression, or a Selector Object, at workflow and step level. `Output(name, ArazzoSelector)` adds
  selector outputs. Outputs have no literal overload because the spec does not allow literal outputs.
- **Workflows.** `Output`, `Parameter`, `OnSuccess` and `OnFailure` fill the workflow's `outputs`,
  `parameters`, `successActions` and `failureActions`, which apply to every step.
- **Components.** `AddComponentInputs`, `AddComponentParameter`, `AddComponentSuccessAction` and
  `AddComponentFailureAction` fill `components`. `ParameterReference`, `OnSuccessReference` and
  `OnFailureReference` refer to those entries from workflows and steps, and `WithInputsReference`
  points a workflow's `inputs` at a shared schema.
- **Extensions.** `WithExtension(name, value)` adds an `x-*` field to the document, a workflow or a
  step. Parameters, actions and selectors carry their own `Extensions`, because they are model objects.

All of this reaches the document before it is validated, so a reference to a missing component, a
literal output, or a 1.1 feature in a document whose `Version` is `ArazzoVersion.V1_0` fails when the
document is first built, at startup or `dotnet getdocument`, not after serialization.

### Transforming the document

`AddDocumentTransformer` registers an `IArazzoDocumentTransformer` (an instance, a type created from the
application services, or a delegate) that changes the document's object model once it is built and
*before* it is validated, as `AsyncApiOptions.AddDocumentTransformer` does for AsyncAPI documents.
Transformers run in registration order, once, and the result is cached. To rewrite the serialized
text instead, for example with an OpenAPI Overlay, use `AddSerializedDocumentTransformer`.

```csharp
options.AddDocumentTransformer((document, context, cancellationToken) =>
{
    document.Info.Description = $"Workflows served by {context.DocumentName}";
    return Task.CompletedTask;
});
```

### Authoring coverage

The Scalar plugin renders every field of Arazzo 1.1 (see [What is rendered](scalar.md#what-is-rendered)),
but rendering is not the same as authoring. The table shows, for each part of the 1.1 model, whether
the fluent API can produce it. Every field in the table is read and written by the JSON and YAML
readers and writers, and rendered by the Scalar plugin.

| Object | Fields the builders author | Not authored by the builders |
|--------|----------------------------|------------------------------|
| Document | `arazzo` (`Version`), `$self`, `info` (with its extensions via `WithInfo(Action)`), `sourceDescriptions`, `workflows`, `components`, `x-*` | — |
| Source Description | `name`, `url`, `type`, `x-*` (`WithSourceDescriptionExtension`) | — |
| Workflow | `workflowId`, `summary`, `description`, `inputs` (schema or `$ref`), `dependsOn`, `steps`, `successActions`, `failureActions`, `outputs`, `parameters`, `x-*` | — |
| Step | every target (`operationId`, `operationPath`, `channelPath` + `action`, `workflowId`, including a workflow of another Arazzo document with `Workflow(sourceName, workflowId)`), `description`, `parameters`, `requestBody`, `successCriteria` (with `x-*` through `SuccessCriteria(ArazzoCriterion)`), `onSuccess`, `onFailure`, `outputs`, `timeout`, `correlationId`, `dependsOn`, `x-*` | — |
| Request Body | `contentType`, `payload` (literal, expression or selector), `replacements`, `x-*` (`WithRequestBodyExtension`) | — |
| Payload Replacement | `target`, `targetSelectorType`, `value` (literal, expression or selector), `x-*` (`Replacement(ArazzoPayloadReplacement)`) | — |
| Parameter, Success Action, Failure Action, Selector, Criterion | every field, by passing the model object (`Parameter(ArazzoParameter)`, `OnSuccess(ArazzoSuccessAction)`, and so on) | — |
| Reusable Object | `reference`, and `value` for parameters | — |
| Components | `inputs`, `parameters`, `successActions`, `failureActions`, `x-*` (`WithComponentsExtension`) | — |

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

### Analyzers

`Bielu.AspNetCore.Arazzo` ships the `BARAZZO` Roslyn analyzers, which check workflow-builder code at
compile time. Most of these mistakes otherwise surface as an exception at startup, and two of them
(BARAZZO006 and BARAZZO007) aren't caught at runtime at all.

| Rule | Severity | Reports |
|------|----------|---------|
| BARAZZO001 | Warning | `AddOpenApi()` called after `AddArazzo()` in the same method, so build-time generation may miss the Arazzo documents (see [Build-time Generation](../build-time-generation.md#alongside-microsoftaspnetcoreopenapi)). |
| BARAZZO002 | Error | A literal workflow id, step id or source name, or a marker type's derived id, that doesn't match `^[A-Za-z0-9_-]+$`. |
| BARAZZO003 | Error | A step that sets no target, or more than one, of `Operation`, `OperationPath`, `Channel` and `Workflow`. |
| BARAZZO004 | Error | A `Channel` step action other than `"send"` or `"receive"` (case-sensitive). |
| BARAZZO005 | Warning | `WithCorrelationId` on a step that doesn't receive: a `send` step, or an operation or workflow step. |
| BARAZZO006 | Warning | A `Channel`, `OperationPath` or `Workflow(sourceName, workflowId)` step naming a source description the document never declares. |
| BARAZZO007 | Warning | `DependsOn` or `Workflow` naming a workflow, or a step of the same workflow, that the document never defines. |
| BARAZZO008 | Info | `AddAsyncApiSource`/`AddOpenApiSource` pointing at a document name the project never registers with `AddAsyncApi`/`AddOpenApi`. |
| BARAZZO009 | Warning | A `Channel` step into a self-wired AsyncAPI source (`AddAsyncApiSource`) naming a channel no `[Channel]` attribute in the project or its references declares. |

The analyzers only read what's written inline: string constants, marker types, and the lambdas passed to
`AddArazzo`, `AddWorkflow` and `Step`. When a check can't see the whole picture, it stays quiet rather than
guess. That happens when a builder is passed to a helper method, an id isn't a constant, a step's lambda
branches, or something other than `AddArazzo` configures `ArazzoOptions` (for example
`Configure<ArazzoOptions>`). Several `AddArazzo` calls with the same document name are merged, as they are
at runtime.

BARAZZO009 resolves a step's source name in the Arazzo document the step belongs to, so a name that one
document self-wires and another points at a partner's URL is only checked where it is self-wired. It then
checks the channel against the AsyncAPI document that source is bound to, using the channels runtime discovery
puts there: a `[Channel]` on a type marked `[AsyncApi]` for that document (or for every document), or on one of
the type's public methods. A `[Channel]` on an unmarked type, or on an `[AsyncApi("v2")]` type when the source
is bound to `v1`, doesn't count.

It compares the step's channel with the document key the AsyncAPI generator gives each `[Channel]`: its
name without the characters outside `[A-Za-z0-9._-]`. So `[Channel("orders/{id}")]` is keyed `ordersid`, and a
step that uses the declared name is reported with the key to use. A channel added at runtime is invisible to
the analyzer, whether Wolverine's message routing or a document transformer adds it. Mark the step with
`dynamic: true` and the analyzer skips it, while startup validation still checks it against the live document:

```csharp
options.AddWorkflow("checkout", wf => wf
    .Step("publish", s => s.Channel("events", "orders", ArazzoStepAction.Send, dynamic: true)));
```

The rule also stays quiet when the project calls `AddWolverine()`, when it declares no `[Channel]` at all, and
when a referenced assembly uses the AsyncAPI attributes but none of its channels can be read from metadata
(they are on non-public members). It skips a step whose AsyncAPI document it can't tell: the Arazzo or AsyncAPI
document name isn't a constant, or the step or its `AddAsyncApiSource` is written outside an `AddArazzo` lambda.

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
