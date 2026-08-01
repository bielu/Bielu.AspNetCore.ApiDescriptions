# Bielu.Cli.Shared

Shared console-CLI infrastructure behind the `dotnet asyncapi`, `dotnet arazzo` and
`dotnet overlay` tools: a prefixed console logger, `--flag value` argument reading, `--file`
glob expansion, and the text/JSON/Markdown renderers for validate and diff reports.

This is an implementation detail of those tools, published because they depend on it. There is no
stability promise for anything here beyond what those tools need — if you are looking for a
general-purpose command-line library, use
[System.CommandLine](https://www.nuget.org/packages/System.CommandLine).

## Documentation

- [Full documentation](https://apidescriptions.bielu.pl/)

## Feedback & Contributing

Released under the [MIT license](https://licenses.nuget.org/MIT). Bug reports and contributions are
welcome at [the GitHub repository](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions).
