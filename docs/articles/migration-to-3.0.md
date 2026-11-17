# Migrating to 3.0

3.0 changes what some documents contain and which documents count as valid, so behavior can change without a compile error. The [changelog](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/blob/main/CHANGELOG.md) lists every change.

## Checklist

1. Update every `Bielu.*` package to 3.0. They are versioned together, so mixing 2.x and 3.0 packages isn't supported.
2. Validate your Arazzo documents against 3.0 (`dotnet arazzo validate`, or start the app); see [Stricter Arazzo validation](#stricter-arazzo-validation).

## Stricter Arazzo validation

`ArazzoValidator` now rejects documents the Arazzo Specification does not allow. It runs in `dotnet arazzo validate`, and when `AddArazzo` builds a document, so an invalid document now fails app startup and `dotnet getdocument` too.

| Now an error | Fix |
|--------------|-----|
| An output (step or workflow) whose value is a literal, such as `"outputs": { "count": 3 }` | Outputs are runtime expressions or Selector Objects: use an expression such as `$response.body#/count` |
| A Selector Object (as a parameter value, payload, payload replacement value or output) in a document declaring `arazzo: 1.0.x` | Declare `1.1.0` (`ArazzoOptions.Version = ArazzoVersion.V1_1`, the default), or use a runtime expression |
| `targetSelectorType` on a payload replacement in a `1.0.x` document | Declare `1.1.0`, or drop it |
