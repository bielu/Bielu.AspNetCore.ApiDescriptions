# Bielu.Arazzo.Cli

`dotnet arazzo` — a .NET tool for validating, linting and diffing
[Arazzo](https://spec.openapis.org/arazzo/latest.html) workflow documents.

## Installation

```sh
dotnet tool install --global Bielu.Arazzo.Cli
```

## Commands

```sh
# Specification validity plus structural invariants
dotnet arazzo validate --file workflows.arazzo.yaml --strict

# Authoring and graph-shape checks
dotnet arazzo lint --file workflows.arazzo.yaml

# Compare two revisions, failing on a breaking change
dotnet arazzo diff --base old.yaml --head new.yaml --fail-on-breaking
```

`validate` covers duplicate ids, mutually exclusive step targets and malformed `inputs` schemas.
`lint` adds what validity does not require: missing summaries and descriptions, identifiers that
travel badly across tooling, circular `dependsOn` graphs, dangling same-document references, and
`components` entries that are declared but never used.

## Documentation

- [Arazzo CLI](https://apidescriptions.bielu.pl/articles/arazzo/cli.html)
- [Full documentation](https://apidescriptions.bielu.pl/)

## Feedback & Contributing

Released under the [MIT license](https://licenses.nuget.org/MIT). Bug reports and contributions are
welcome at [the GitHub repository](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions).
