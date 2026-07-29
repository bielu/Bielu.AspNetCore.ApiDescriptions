# CLI Tool

The `dotnet overlay` CLI applies [OpenAPI Overlays](https://spec.openapis.org/overlay/latest.html) to API
descriptions, and validates overlay documents on their own.

Because the engine underneath works on a `System.Text.Json.Nodes.JsonNode` tree rather than a typed
object model, the `--file` it transforms can be **OpenAPI, AsyncAPI, or Arazzo** — see the
[overview](overview.md) for what that does and does not claim about specification conformance.

## Installation

```bash
dotnet tool install -g Bielu.Overlay.Cli
```

## Commands

### `apply`

Apply one or more overlays to a description.

```bash
dotnet overlay apply --file asyncapi.json --overlay public.overlay.yaml --output public.json --strict
```

**Options:**
- `--file <path>` - The description to transform (required)
- `--overlay <path>` - An overlay to apply (required, repeatable)
- `--output <path>` - Where to write the result (default: standard output)
- `--format <json|yaml>` - Output format (default: inferred from `--output`'s extension, else `json`)
- `--strict` - Treat a `target` matching zero nodes as an error

Multiple `--overlay` arguments are applied **in the order given**, each against the result of the last —
the same sequencing the specification requires of actions within a single overlay. So this:

```bash
dotnet overlay apply --file api.yaml \
  --overlay strip-internal.yaml \
  --overlay add-partner-metadata.yaml \
  --output partner.yaml
```

strips first, then annotates what survived.

Input format is detected from the content, so JSON and YAML descriptions are both accepted. Output
format follows `--format` if given, otherwise the `--output` extension (`.yaml`/`.yml` → YAML), matching
how `dotnet asyncapi merge` chooses. Writing to standard output defaults to JSON, so the command pipes
cleanly:

```bash
dotnet overlay apply --file api.json --overlay public.yaml | jq '.info'
```

Nothing is written when application reports errors — a partially transformed document is never left
behind for a later build step to pick up.

#### Why `--strict` in CI

The specification permits a `target` that matches nothing; it is simply a no-op. That is convenient
while authoring and dangerous in a pipeline, where it almost always means the overlay has drifted out of
sync with the description it transforms. `--strict` turns those into errors so the build fails instead
of silently publishing an untransformed document.

### `validate`

Check overlay documents without a target description in hand — required fields, at least one action,
`target`/`copy` expressions that parse as RFC 9535 JSONPath, and `copy` used only where the declared
version supports it.

```bash
dotnet overlay validate --file overlays/*.yaml --strict
```

**Options:**
- `--file <path>` - Path or glob to overlay document(s) (required, repeatable)
- `--strict` - Treat warnings as errors
- `--format <text|json>` - Output format (default: text)

Warnings cover overlays that are legal but pointless — an action setting none of `update`/`copy`/`remove`,
or setting several where only the highest-precedence one can take effect.

## Exit codes

| Code | Meaning |
|------|---------|
| 0 | Success — for `validate`, no errors (and no warnings under `--strict`) |
| 1 | Errors were reported, a required argument was missing, or a file could not be read |
