# Bielu.Arazzo.NET.Readers

JSON and YAML readers for the [Arazzo Specification](https://spec.openapis.org/arazzo/latest.html),
producing [Bielu.Arazzo.NET](https://www.nuget.org/packages/Bielu.Arazzo.NET) documents.

Kept as a separate package so `YamlDotNet` stays out of the model package's dependency graph — code
that only builds and writes documents does not pay for a YAML parser.

## Installation

```sh
dotnet add package Bielu.Arazzo.NET.Readers
```

## Usage

```csharp
var result = ArazzoStringReader.Read(text);   // JSON or YAML

foreach (var error in result.Diagnostics.Errors)
{
    Console.Error.WriteLine(error);
}

ArazzoDocument? document = result.Document;
```

Readers report problems as diagnostics rather than throwing, so malformed input can be surfaced to a
user instead of taking the process down. JSON and YAML share one deserializer.

## Documentation

- [Arazzo overview](https://apidescriptions.bielu.pl/articles/arazzo/overview.html)
- [Full documentation](https://apidescriptions.bielu.pl/)

## Feedback & Contributing

Released under the [MIT license](https://licenses.nuget.org/MIT). Bug reports and contributions are
welcome at [the GitHub repository](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions).
