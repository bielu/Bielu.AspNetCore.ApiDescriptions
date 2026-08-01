# Bielu.AspNetCore.AsyncApi.Attributes

The attributes used to describe AsyncAPI channels, operations, messages and parameters for
[Bielu.AspNetCore.AsyncApi](https://www.nuget.org/packages/Bielu.AspNetCore.AsyncApi).

This package is deliberately separate and free of ASP.NET Core dependencies, so a shared contracts
assembly can be annotated without dragging the web stack into its dependency graph. The application
that references `Bielu.AspNetCore.AsyncApi` then discovers those annotations at generation time.

## Installation

```sh
dotnet add package Bielu.AspNetCore.AsyncApi.Attributes
```

## Usage

```csharp
[AsyncApi]
[Channel("light/measured")]
public class StreetlightMessageBus
{
    [PublishOperation(typeof(LightMeasuredEvent), Summary = "Inform about environmental lighting conditions.")]
    public void PublishLightMeasuredEvent(LightMeasuredEvent e) { }
}
```

## Analyzers

The package also ships the `BASYNC` Roslyn analyzers, which catch attribute misuse that would
otherwise fail silently at generation time — an operation attribute with no `[Channel]`, a
`[Channel]` on a type with no `[AsyncApi]`, duplicate message names, malformed example JSON, and
identifiers the AsyncAPI specification discourages.

## Documentation

- [Attributes reference](https://apidescriptions.bielu.pl/articles/attributes.html)
- [Full documentation](https://apidescriptions.bielu.pl/)

## Feedback & Contributing

Released under the [MIT license](https://licenses.nuget.org/MIT). Bug reports and contributions are
welcome at [the GitHub repository](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions).
