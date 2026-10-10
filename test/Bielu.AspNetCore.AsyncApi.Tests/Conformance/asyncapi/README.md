# AsyncAPI JSON Schemas

These files are **vendored verbatim** from the AsyncAPI Initiative's
[spec-json-schemas](https://github.com/asyncapi/spec-json-schemas) repository, at commit
[`21e5be6`](https://github.com/asyncapi/spec-json-schemas/tree/21e5be6d86a6c337808b6b07fc5fc342ef6d9d49).
They are licensed Apache-2.0 by the AsyncAPI Initiative. Do not hand-edit them — refresh them from
upstream instead, and bump the commit recorded above.

They are vendored rather than fetched at test time so the suite runs offline and a network outage or an
upstream change can never turn into a red build.

| File | Upstream path |
|------|---------------|
| `2.6.0.json` | `schemas/2.6.0-without-$id.json` |
| `3.1.0.json` | `schemas/3.1.0-without-$id.json` |

The `without-$id` variants are the bundled, self-contained form of each schema, so no reference has to be
resolved over the network. There is one file per version string the generator declares (see
`AsyncApiDocumentService`); when it starts declaring another, add the matching schema here.

Driven by `AsyncApiOfficialSchemaConformanceTests`, which serves a document, reads the `asyncapi` version
it declares, and validates it against the schema of that name.
