# CLI Tool

The `dotnet arazzo` CLI tool provides commands for validating, linting, and diffing Arazzo workflow
documents, and for finding the workflows an API change affects. It is a separate tool from `dotnet asyncapi` so `arazzo` stays independently discoverable on
NuGet, but it reuses the same underlying infrastructure (`Bielu.Cli.Shared`) and mirrors its command
shapes and output formats.

## Installation

```bash
dotnet tool install -g Bielu.Arazzo.Cli
```

## Commands

### `validate`

Validate Arazzo documents against the specification's structural invariants — unique
`workflowId`/`stepId` values, mutually-exclusive step targets, unknown enum values, and malformed
`inputs` JSON Schema.

```bash
dotnet arazzo validate --file ./workflows/*.yaml --strict
```

**Options:**
- `--file <path>` - Path or glob to Arazzo document(s) (required, repeatable)
- `--source <name>=<path>` - Check step references against the OpenAPI/AsyncAPI file (JSON or YAML) for
  the source description `<name>` (repeatable)
- `--resolve-local-sources` - Also load every source description whose `url` is a relative path to an
  existing file, resolved from the Arazzo document's folder
- `--strict` - Treat warnings as errors
- `--format <text|json>` - Output format (default: text)

#### Checking references against the source documents

With `--source` or `--resolve-local-sources`, `validate` also checks that every step's `operationId`,
`operationPath` and `channelPath` exists in the document it targets. It is the same check
`Bielu.AspNetCore.Arazzo` runs at startup (`ValidateSourceReferencesOnStartup`), run against files instead
of a live app. That makes it usable where the app can't start, and for Arazzo documents written by hand or
kept in another repository.

```bash
# The documents an app writes at build time
dotnet arazzo validate --file obj/docs/MyApp_arazzo-v1.json \
  --source shop=obj/docs/MyApp.json \
  --source events=obj/docs/MyApp_asyncapi-v1.json

# A hand-written document whose sourceDescriptions point at files next to it
dotnet arazzo validate --file api/checkout.arazzo.yaml --resolve-local-sources
```

```text
error:   Error: checkout.notify: channelPath '{$sourceDescriptions.events.url}#/channels/orders' did not resolve. (at /workflows/0/steps/1/channelPath)
```

A source's kind comes from its `type`, or, when it has none, from the document's `openapi`/`asyncapi`
field. An AsyncAPI `operationId` is an AsyncAPI 3 `operations` key or an AsyncAPI 2 `publish`/`subscribe`
`operationId`. A declared source with no file, such as another service's API reached by URL, is skipped,
and the text output lists it. A reference to a source the document doesn't declare is always an error.

### `lint`

Lint Arazzo documents for style and graph-shape issues that `validate` doesn't cover: missing
summaries/descriptions, identifiers with characters that don't travel well across tooling, circular
`dependsOn` graphs (at both the step and workflow level), same-document `dependsOn` references that
don't resolve to a real id, and `components` entries that are declared but never referenced.

```bash
dotnet arazzo lint --file ./workflows/*.yaml
```

**Options:**
- `--file <path>` - Path or glob to Arazzo document(s) (required, repeatable)
- `--strict` - Treat warnings as errors
- `--format <text|json>` - Output format (default: text)

`lint` only checks what can be determined from the document itself. Whether an `operationId` really exists
in the linked OpenAPI/AsyncAPI document is checked by `validate --source` (see above).

### `diff`

Compare two Arazzo documents and identify breaking and non-breaking changes: removed/added workflows
and steps, a step's target changing (`operationId`/`operationPath`/`channelPath`/`workflowId`), a step's
`action` changing, and source description url/type changes.

```bash
dotnet arazzo diff --base old.yaml --head new.yaml --fail-on-breaking
```

**Options:**
- `--base <path>` - Path to the base (old) Arazzo document (required)
- `--head <path>` - Path to the head (new) Arazzo document (required)
- `--fail-on-breaking` - Exit with code 1 if breaking changes are detected
- `--format <text|json|markdown>` - Output format (default: text)

### `impact`

List the workflow steps an OpenAPI or AsyncAPI change affects. `diff` compares two versions of the Arazzo
document. `impact` keeps the Arazzo document fixed and compares two versions of the APIs it calls, the way a
pull request that changes an endpoint or a message does. Every step's target is resolved against the source
documents before the change (`--base`) and after it (`--head`):

| Before | After | Reported as |
| --- | --- | --- |
| resolves | doesn't resolve | **Breaking**: the operation or channel was removed or renamed |
| resolves | resolves, but its definition changed | Non-breaking, for review: its parameters, request body, responses, messages or payload schemas changed |
| doesn't resolve | resolves | Non-breaking |
| doesn't resolve | doesn't resolve | A warning: the workflow was already broken, which `validate --source` reports |

A definition counts as changed after its local `$ref`s are followed, so a payload schema edited under
`components` is caught at the step that sends or receives it. Property order and documentation-only keywords
(`description`, `summary`, `title`, examples, `externalDocs`, `x-` extensions) are ignored.

```bash
dotnet arazzo impact --file checkout.arazzo.json \
  --base shop=base/openapi.json --base events=base/asyncapi.json \
  --head shop=openapi.json      --head events=asyncapi.json \
  --fail-on-breaking --format markdown
```

```text
info: Breaking:
error:   - checkout.notify: channelPath '{$sourceDescriptions.events.url}#/channels/orders' did not resolve. It resolved before the change.
info: NonBreaking:
info:   - checkout.order: The operation '$sourceDescriptions.shop.createOrder' targets changed in source 'shop'. Review the step's parameters, request body, success criteria and outputs.
```

**Options:**
- `--file <path>` - Path to the Arazzo document (required)
- `--base <name>=<path>` - The source description `<name>`'s OpenAPI/AsyncAPI document before the change (repeatable, at least one)
- `--head <name>=<path>` - The same source's document after the change (repeatable, at least one)
- `--fail-on-breaking` - Exit with code 1 if a step's target no longer resolves
- `--format <text|json|markdown>` - Output format (default: text); `markdown` suits a pull-request comment

A source given only with `--base` or only with `--head` can't be compared, so its steps are skipped with a
warning. An unqualified `operationId` is looked up in every source given, so qualify it
(`$sourceDescriptions.<name>.<operationId>`) or pass every source it might live in.
