# Migrating to 3.0

3.0 removes API that 2.x marked obsolete, and changes what some documents contain and which documents count as valid, so some changes show up as compile errors and others only at runtime. The [changelog](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/blob/main/CHANGELOG.md) lists every change.

## Checklist

1. Update every `Bielu.*` package to 3.0. They are versioned together, so mixing 2.x and 3.0 packages isn't supported.
2. Validate your Arazzo documents against 3.0 (`dotnet arazzo validate`, or start the app); see [Stricter Arazzo validation](#stricter-arazzo-validation).
3. Fix the compile errors from the [removed members](#removed-members).
4. If you use `AddAsyncApiForApiVersions()`, check each version's AsyncAPI document; see [API versioning: documents are filtered by version](#api-versioning-documents-are-filtered-by-version).

## Stricter Arazzo validation

`ArazzoValidator` now rejects documents the Arazzo Specification does not allow. It runs in `dotnet arazzo validate`, and when `AddArazzo` builds a document, so an invalid document now fails app startup and `dotnet getdocument` too.

| Now an error | Fix |
|--------------|-----|
| An output (step or workflow) whose value is a literal, such as `"outputs": { "count": 3 }` | Outputs are runtime expressions or Selector Objects: use an expression such as `$response.body#/count` |
| A Selector Object (as a parameter value, payload, payload replacement value or output) in a document declaring `arazzo: 1.0.x` | Declare `1.1.0` (`ArazzoOptions.Version = ArazzoVersion.V1_1`, the default), or use a runtime expression |
| `targetSelectorType` on a payload replacement in a `1.0.x` document | Declare `1.1.0`, or drop it |
| In a `1.0.x` document, the other 1.1 additions: an `asyncapi` source description; the `channelPath`, `action`, `correlationId`, `timeout` or `dependsOn` step fields; the `querystring` parameter location; the `jsonpointer` criterion type or the `rfc9535` JSONPath version | Declare `1.1.0`. A document that uses AsyncAPI steps already needs 1.1, which introduced them |
| `$self` in a `1.0.x` document, or a `$self` with a `#fragment` | Declare `1.1.0`; remove the fragment |
| A Reusable Object (`$components.{kind}.{name}`) that names no entry, or an entry of another kind (a `successActions` reference in `onFailure`, say), or a workflow `inputs` `$ref` to a missing `#/components/inputs/{name}` | Add the component, or fix the reference |
| An output name outside `^[a-zA-Z0-9.\-_]+$`, such as one with a space or a `/`. `ArazzoStepBuilder.Output` now throws for one, as the workflow builder already did | Rename the output, and the `$steps.{id}.outputs.{name}` expressions that read it |
| `value` on a reference to anything but a parameter | Remove `value`; only a parameter reference can override its value |
| A component parameter or action that is invalid the way an inline one would be (a `goto` action with no target, an unknown parameter `in`) | Fix the component |

## API versioning: documents are filtered by version

In 2.x, `AddAsyncApiForApiVersions()` gave every version's document every channel. In 3.0 each version's document holds only the declarations of that version, as described in [One document per API version](multiple-documents.md#5-one-document-per-api-version):

- A versioned controller action or minimal API handler follows the version metadata Asp.Versioning gives its endpoint, conventions included.
- Any other `[AsyncApi]` class or method follows its `[ApiVersion]`, `[MapToApiVersion]` and `[ApiVersionNeutral]` attributes, a method's own winning over its class's.
- Version-neutral and unversioned declarations stay in every document.

What to check:

- A channel that is missing from a version it belongs to: declare that version on its class or method, or mark it `[ApiVersionNeutral]` to keep it in every version.
- An unversioned controller in an app with `AssumeDefaultVersionWhenUnspecified`: Asp.Versioning maps it to the default version, so its channels now appear only in that version's document. Mark it `[ApiVersionNeutral]` if it belongs to all of them.
- `AddIncludedChannel` calls in the `configure` callback that only existed to work around the missing filtering can go.

## Removed members

| Removed | Use instead |
|---------|-------------|
| `AsyncApiOptions.DocumentRoutePattern` (obsolete since 2.0; nothing read it) | Nothing: the route is the pattern passed to `MapAsyncApi(pattern)` |
| `[PublishOperation]` (obsolete since 2.0) | `[ReceiveOperation]`: a 2.x publish operation is one where the application receives |
| `[SubscribeOperation]` (obsolete since 2.0) | `[SendOperation]`: a 2.x subscribe operation is one where the application sends |

## Typed Arazzo source resolvers

`IArazzoSourceResolver` gains `DocumentType`, and the new `ArazzoSourceResolver<TDocument>` base class implements the interface for one document type: override `SourceType` and the typed `TryResolve*` methods, and the base class checks the document's type for you. A document of the wrong type now throws `ArgumentException` naming both types, instead of quietly resolving nothing.

- A custom resolver: derive from `ArazzoSourceResolver<TDocument>` instead of implementing `IArazzoSourceResolver` directly. `TryResolveOperationPath` and `TryResolveChannelPath` are virtual and return `false` unless overridden.
- Code that calls `AsyncApiSourceResolver` or `OpenApiSourceResolver` directly: their methods now take an `AsyncApiDocument` or an `OpenApiDocument` rather than `object`. `ArazzoWorkspace` is unchanged.
