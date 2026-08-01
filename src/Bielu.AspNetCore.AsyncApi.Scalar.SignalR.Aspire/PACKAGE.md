# Bielu.AspNetCore.AsyncApi.Scalar.SignalR.Aspire

Adds the interactive **SignalR console** to a [Scalar.Aspire](https://www.nuget.org/packages/Scalar.Aspire)
API Reference resource from your .NET Aspire AppHost.

## Installation

```sh
dotnet add package Bielu.AspNetCore.AsyncApi.Scalar.SignalR.Aspire
```

## Usage

```csharp
var api = builder.AddProject<Projects.MyApi>("api");

builder.AddScalar("scalar")
    .WithSignalRClient();
```

The console registers itself through Scalar's `PluginUrls` option, which loads it as an ES module
before the API Reference mounts. The Scalar container keeps its own bundle and only the plugin is
added — so several consoles can be enabled on the same resource, and none of them pins the container
to a particular Scalar version.

By default the plugin is loaded from jsDelivr, pinned to the exact npm version released alongside
this package. Pass `pluginUrl:` to load it from somewhere else, and `configure:` to name the
AsyncAPI documents explicitly instead of discovering them from the Scalar configuration. Those
document URLs are resolved by the browser, so they must be reachable from the Scalar page's origin.

## Documentation

- [Scalar consoles](https://apidescriptions.bielu.pl/articles/scalar-consoles.html)
- [Full documentation](https://apidescriptions.bielu.pl/)

## Feedback & Contributing

Released under the [MIT license](https://licenses.nuget.org/MIT). Bug reports and contributions are
welcome at [the GitHub repository](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions).
