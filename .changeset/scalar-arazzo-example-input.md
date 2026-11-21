---
"@bielu/scalar-arazzo": minor
"bielu-aspnetcore-asyncapi": minor
---

Each workflow in the Arazzo view in Scalar offers an Example input under its inputs: a JSON object made from the inputs schema, with a copy button. It uses the schema's `example`, `examples`, `const`, `default` or `enum` where there is one, otherwise a value of its type (samples for known string formats, the minimum for numbers, the number for a .NET number also read from a string), following `$ref`s into the schema and `components.inputs`.
