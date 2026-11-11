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

**Target runtime.** The tool ships builds for `net10.0` and `net11.0`, and generates each document on the
runtime the application targets. It reads `runtimeOptions.tfm` from the app's `<app>.runtimeconfig.json`. When that
runtime is not the one the CLI is running on, the CLI relaunches itself on the app's runtime (`dotnet exec` with the
app's `runtimeconfig.json` and `deps.json`), the way `dotnet getdocument` does. The relaunched run uses the tool's
build for the highest target framework that doesn't exceed the app's, and the app's own copies of its
dependencies. Arguments, output and exit codes are the same either way. The app's runtime must be installed; if it
isn't, the .NET host says so. Apps that target a framework older than `net10.0` are rejected with the list of
supported ones.

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
