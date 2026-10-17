# Bielu.AspNetCore.ApiDescriptions

Shared build-time document generation for the Bielu API-description packages.

`dotnet getdocument` (run by `Microsoft.Extensions.ApiDescription.Server` during `dotnet build`) asks a
single `IDocumentProvider` for every document an app serves. This package owns that provider: it writes
`Microsoft.AspNetCore.OpenApi`'s documents and those of every registered `IBuildTimeDocumentSource`
(AsyncAPI, Arazzo, ...), and settles their file names so two documents never land in the same file.

Packages that describe a new document family register a source:

```csharp
services.AddBuildTimeDocumentSource<MyBuildTimeDocumentSource>();
```

This is an implementation detail of `Bielu.AspNetCore.AsyncApi` and `Bielu.AspNetCore.Arazzo`, published
because they depend on it.

## Documentation

- [Full documentation](https://apidescriptions.bielu.pl/)

## Feedback & Contributing

Released under the [MIT license](https://licenses.nuget.org/MIT). Bug reports and contributions are
welcome at [the GitHub repository](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions).
