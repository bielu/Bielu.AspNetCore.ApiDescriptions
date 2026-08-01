# Bielu.AspNetCore.AsyncApi.Cli

`dotnet asyncapi` — a .NET tool for working with the AsyncAPI documents your application produces.

## Installation

```sh
dotnet tool install --global Bielu.AspNetCore.AsyncApi.Cli
```

## Commands

```sh
# Generate documents from a built ASP.NET Core assembly
dotnet asyncapi getdocument --assembly MyApi --assembly-path ./bin/Release/net10.0/MyApi.dll --output ./docs

# Validate one or more documents (--strict also fails on warnings)
dotnet asyncapi validate --file asyncapi.json --strict --format json

# Compare two documents and fail the build on a breaking change
dotnet asyncapi diff --base old.json --head new.json --fail-on-breaking --format markdown

# Merge documents from several services into one
dotnet asyncapi merge --source a.json --source b.json --output merged.json
```

`validate` and `diff` exit non-zero on failure, so they drop straight into CI. `diff` classifies
a removed channel, operation or message — and any narrowing of a payload schema — as breaking.

## Documentation

- [CLI reference](https://apidescriptions.bielu.pl/articles/cli.html)
- [Full documentation](https://apidescriptions.bielu.pl/)

## Feedback & Contributing

Released under the [MIT license](https://licenses.nuget.org/MIT). Bug reports and contributions are
welcome at [the GitHub repository](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions).
