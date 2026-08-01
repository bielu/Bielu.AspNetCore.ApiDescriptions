# Bielu.AspNetCore.AsyncApi.Scalar

The shared foundation the Bielu interactive Scalar consoles are built on — document options, embedded
plugin-bundle endpoint mapping, and the `ScalarOptions` plumbing that injects a console into a
Scalar API Reference.

You normally do not install this directly. Install the console for the protocol you want:

- [Bielu.AspNetCore.AsyncApi.Scalar.SignalR](https://www.nuget.org/packages/Bielu.AspNetCore.AsyncApi.Scalar.SignalR)
- [Bielu.AspNetCore.AsyncApi.Scalar.Grpc](https://www.nuget.org/packages/Bielu.AspNetCore.AsyncApi.Scalar.Grpc)

Reference it directly only when building a console for a protocol of your own, in which case
`ScalarPluginDocumentOptions<TSelf>`, `MapScalarPluginBundle(...)` and
`WithAsyncApiPluginScript(...)` are the extension points.

## Documentation

- [Scalar consoles](https://apidescriptions.bielu.pl/articles/scalar-consoles.html)
- [Full documentation](https://apidescriptions.bielu.pl/)

## Feedback & Contributing

Released under the [MIT license](https://licenses.nuget.org/MIT). Bug reports and contributions are
welcome at [the GitHub repository](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions).
