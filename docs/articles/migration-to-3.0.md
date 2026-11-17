# Migrating to 3.0

3.0 changes what some documents contain and which documents count as valid, so behavior can change without a compile error. The [changelog](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/blob/main/CHANGELOG.md) lists every change.

## Checklist

1. Update every `Bielu.*` package to 3.0. They are versioned together, so mixing 2.x and 3.0 packages isn't supported.
2. Validate your Arazzo documents against 3.0 (`dotnet arazzo validate`, or start the app); see [Stricter Arazzo validation](#stricter-arazzo-validation).
3. If you use `AddAsyncApiForApiVersions()`, check each version's AsyncAPI document; see [API versioning: documents are filtered by version](#api-versioning-documents-are-filtered-by-version).

## Stricter Arazzo validation

`ArazzoValidator` now rejects documents the Arazzo Specification does not allow. It runs in `dotnet arazzo validate`, and when `AddArazzo` builds a document, so an invalid document now fails app startup and `dotnet getdocument` too.

| Now an error | Fix |
|--------------|-----|
| An output (step or workflow) whose value is a literal, such as `"outputs": { "count": 3 }` | Outputs are runtime expressions or Selector Objects: use an expression such as `$response.body#/count` |
| A Selector Object (as a parameter value, payload, payload replacement value or output) in a document declaring `arazzo: 1.0.x` | Declare `1.1.0` (`ArazzoOptions.Version = ArazzoVersion.V1_1`, the default), or use a runtime expression |
| `targetSelectorType` on a payload replacement in a `1.0.x` document | Declare `1.1.0`, or drop it |
| `$self` in a `1.0.x` document, or a `$self` with a `#fragment` | Declare `1.1.0`; remove the fragment |
| A Reusable Object (`$components.{kind}.{name}`) that names no entry, or an entry of another kind (a `successActions` reference in `onFailure`, say), or a workflow `inputs` `$ref` to a missing `#/components/inputs/{name}` | Add the component, or fix the reference |
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
