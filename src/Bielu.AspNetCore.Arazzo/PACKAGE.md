# Bielu.AspNetCore.Arazzo

ASP.NET Core integration for [Bielu.Arazzo.NET](https://www.nuget.org/packages/Bielu.Arazzo.NET):
author [Arazzo](https://spec.openapis.org/arazzo/latest.html) workflows with a fluent builder, serve
them from your app, and have their references checked against the documents that same app produces.

## Installation

```sh
dotnet add package Bielu.AspNetCore.Arazzo
```

## Usage

```csharp
builder.Services.AddArazzo(options =>
{
    options.WithInfo("Streetlights workflows", "1.0.0");

    options.AddWorkflow("measureAndAlert", wf => wf
        .Step("publishMeasurement", s => s
            .Operation("$sourceDescriptions.events.sendLightMeasurement")
            .Output("measurementId", "$message.payload#/id")));
});

var app = builder.Build();

app.MapArazzo();   // -> /arazzo/{documentName}.json | .yaml
```

## Cross-spec reference validation

The differentiating feature: `sourceDescriptions` can be resolved against the **live documents of
the same application** — its AsyncAPI documents and its `Microsoft.AspNetCore.OpenApi` document — so
every `operationId` and `channelPath` a step references is checked **at startup** rather than
failing in production. Rename a channel and the app fails to start, instead of the workflow silently
pointing at nothing.

## Documentation

- [Arazzo overview](https://apidescriptions.bielu.pl/articles/arazzo/overview.html)
- [Full documentation](https://apidescriptions.bielu.pl/)

## Feedback & Contributing

Released under the [MIT license](https://licenses.nuget.org/MIT). Bug reports and contributions are
welcome at [the GitHub repository](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions).
