# Bielu.AspNetCore.AsyncApi.Versioning

[Asp.Versioning](https://www.nuget.org/packages/Asp.Versioning.Mvc.ApiExplorer) integration for
[Bielu.AspNetCore.AsyncApi](https://www.nuget.org/packages/Bielu.AspNetCore.AsyncApi): one AsyncAPI
document per API version, matching the convention already used on the OpenAPI side.

## Installation

```sh
dotnet add package Bielu.AspNetCore.AsyncApi.Versioning
```

## Usage

```csharp
builder.Services.AddApiVersioning().AddApiExplorer();

builder.Services.AddAsyncApiForApiVersions((options, description) =>
{
    options.WithInfo($"My API {description.ApiVersion}", description.ApiVersion.ToString());
});

var app = builder.Build();

app.MapAsyncApi();   // -> /asyncapi/v1.json, /asyncapi/v2.json, ...
```

Every version the API explorer reports gets its own document, and versions marked deprecated are
flagged as deprecated in the generated output.

## Documentation

- [Multiple documents](https://apidescriptions.bielu.pl/articles/multiple-documents.html)
- [Full documentation](https://apidescriptions.bielu.pl/)

## Feedback & Contributing

Released under the [MIT license](https://licenses.nuget.org/MIT). Bug reports and contributions are
welcome at [the GitHub repository](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions).
