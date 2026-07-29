---
"bielu-aspnetcore-asyncapi": minor
---

Added `Bielu.Arazzo.Cli` (tool command `dotnet-arazzo`), a separate CLI tool for Arazzo workflow documents with `validate`, `lint`, and `diff` commands — kept separate from `dotnet asyncapi` so `arazzo` stays independently discoverable on NuGet. `validate` runs the reader diagnostics plus `ArazzoValidator`'s structural invariants. `lint` is new: style and graph-shape checks beyond structural validation — missing summaries/descriptions, identifiers with characters that don't travel well across tooling, circular `dependsOn` graphs (step- and workflow-level), dangling same-document `dependsOn` references, and `components` entries that are declared but never referenced. `diff` compares two documents and classifies added/removed workflows and steps, step target/action changes, and source-description changes as breaking or non-breaking, with text/JSON/markdown output and `--fail-on-breaking`.

Also extracted `Bielu.Cli.Shared`, the console-CLI infrastructure (`ConsoleCliLogger`, `CliArgumentReader`, `CliFileResolver`, and the `validate`/`diff` report renderers) that the CLI tools would otherwise have duplicated. `Bielu.AspNetCore.AsyncApi.Cli` now builds on the same shared package; its behavior and tests are unchanged, and `Bielu.Overlay.Cli` (below) is built on it from the start.
