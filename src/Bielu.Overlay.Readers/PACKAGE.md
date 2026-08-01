# Bielu.Overlay.NET.Readers

JSON and YAML readers for the [OpenAPI Overlay Specification](https://spec.openapis.org/overlay/latest.html),
producing [Bielu.Overlay.NET](https://www.nuget.org/packages/Bielu.Overlay.NET) documents.

A separate package so `YamlDotNet` stays out of the engine's dependency graph.

## Installation

```sh
dotnet add package Bielu.Overlay.NET.Readers
```

## Usage

```csharp
var result = OverlayStringReader.Read(text);   // JSON or YAML, auto-detected

if (result.HasErrors)
{
    foreach (var diagnostic in result.Diagnostics)
    {
        Console.Error.WriteLine(diagnostic);
    }
}

OverlayDocument? overlay = result.Document;
```

Malformed input produces diagnostics rather than exceptions.

## Documentation

- [Overlay overview](https://apidescriptions.bielu.pl/articles/overlay/overview.html)
- [Full documentation](https://apidescriptions.bielu.pl/)

## Feedback & Contributing

Released under the [MIT license](https://licenses.nuget.org/MIT). Bug reports and contributions are
welcome at [the GitHub repository](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions).
