---
"bielu-aspnetcore-asyncapi": patch
---

The Arazzo view in Scalar now says which spec each step targets: a badge such as `OpenAPI 3.1.1`, `AsyncAPI 3.0.0` or `Workflow` on every step target (taken from the loaded document, or from the source description's `type` when it is not loaded), whether a channel step sends or receives, a colored marker per spec on the overview diagram with a legend, and each source description's spec, load state and step count in the header.
