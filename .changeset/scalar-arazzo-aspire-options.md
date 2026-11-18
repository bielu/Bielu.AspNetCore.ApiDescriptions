---
"@bielu/scalar-arazzo": patch
"bielu-aspnetcore-asyncapi": minor
---

`Bielu.AspNetCore.Arazzo.Scalar.Aspire` gets `WithArazzoOptions(arazzo => …)`, which sets the Arazzo view's `EndLabelExtension`, `PagesExtension`, `PageExtension` and `LabelExtension` on a Scalar.Aspire resource by adding them to the plugin module's URL, keeping a custom URL's other query parameters and fragment. Every input path now accepts the same extension names, `x-` followed by a name: .NET rejects a bare `x-`, and the npm options, the page global and the module URL drop a non-extension `endLabelExtension` as they already did for the Pages options.
