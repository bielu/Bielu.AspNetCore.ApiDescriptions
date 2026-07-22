# CLI Tool

The `dotnet asyncapi` CLI tool provides commands for generating, merging, validating, and diffing AsyncAPI documents.

## Installation

```bash
dotnet tool install -g Bielu.AspNetCore.AsyncApi.Cli
```

## Commands

### `getdocument`

Generate AsyncAPI documents from a built .NET assembly.

```bash
dotnet asyncapi getdocument \
    --assembly MyApp \
    --assembly-path bin/Debug/net10.0/MyApp.dll \
    --output ./docs \
    --project MyApp
```

**Options:**
- `--assembly <name>` - Assembly name to load (required)
- `--assembly-path <path>` - The full path to the assembly
- `--output <dir>` - Output directory for generated documents (required)
- `--project <name>` - Project name for file naming
- `--document <name>` - Generate only the specified document
- `--file-name <name>` - Override file name (without extension)

### `merge`

Merge multiple AsyncAPI documents into a single document.

```bash
dotnet asyncapi merge \
    --source ./docs/service-a.json \
    --source https://example.com/service-b.json \
    --output ./docs/merged.json \
    --title "Unified API"
```

**Options:**
- `--source <uri>` - A document source URI (file or URL) (required, repeatable)
- `--output <path>` - The output file path (required)
- `--prefix <prefix>` - Key prefix for the corresponding source (optional, repeatable)
- `--title <title>` - Title for the merged document
- `--version <version>` - Version for the merged document

### `validate`

Validate AsyncAPI documents against the AsyncAPI specification.

```bash
dotnet asyncapi validate --file ./docs/*.json --strict
```

**Options:**
- `--file <path>` - Path or glob to AsyncAPI document(s) (required, repeatable)
- `--strict` - Treat warnings as errors
- `--format <text|json>` - Output format (default: text)

### `diff`

Compare two AsyncAPI documents and identify breaking and non-breaking changes.

```bash
dotnet asyncapi diff --base old.json --head new.json --fail-on-breaking
```

**Options:**
- `--base <path>` - Path to the base (old) AsyncAPI document (required)
- `--head <path>` - Path to the head (new) AsyncAPI document (required)
- `--fail-on-breaking` - Exit with code 1 if breaking changes are detected
- `--format <text|json|markdown>` - Output format (default: text)
