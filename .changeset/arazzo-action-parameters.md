---
"bielu-aspnetcore-asyncapi": major
---

Arazzo success actions support the `parameters` field Arazzo 1.1.0 added: `ArazzoSuccessAction.Parameters` is read, validated and written in JSON and YAML. Before, the reader kept the field as an extension and both writers threw on it. `ArazzoValidator` now rejects `parameters` on success and failure actions (inline and component) in a document declaring Arazzo 1.0, and rejects an `in` on an action parameter, which the spec forbids. Parameter references in success actions are checked against `components.parameters`, as failure-action references already were.
