# CLI Tool

The `dotnet arazzo` CLI tool provides commands for validating, linting, and diffing Arazzo workflow
documents. It is a separate tool from `dotnet asyncapi` so `arazzo` stays independently discoverable on
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
- `--strict` - Treat warnings as errors
- `--format <text|json>` - Output format (default: text)

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

Reference resolution against the actual source documents (does this `operationId` really exist in the
linked OpenAPI/AsyncAPI document?) needs a running app and is `Bielu.AspNetCore.Arazzo`'s
`IArazzoSourceResolver` territory — `lint` only checks what can be determined from the document itself.

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
