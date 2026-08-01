# Bielu.AspNetCore.AsyncApi.Scalar.SignalR

An interactive **SignalR console** for the [Scalar](https://scalar.com/) API Reference in ASP.NET
Core. It reads the `signalr` bindings in your AsyncAPI document and lets you connect to hubs, invoke methods and subscribe to server events — from the
same page that documents them.

## Installation

```sh
dotnet add package Bielu.AspNetCore.AsyncApi.Scalar.SignalR
dotnet add package Scalar.AspNetCore
```

## Usage

```csharp
app.MapScalarSignalRAssets();

app.MapScalarApiReference(options =>
{
    options.AddAsyncApiDocument("v1", "My API", "/asyncapi/v1.json");
    options.WithSignalRClient();
});
```

Requests are prefilled from your payload schemas (and from message examples, when the document
declares them), and Scalar's configured authentication is passed through to the connection.

Using .NET Aspire? Install
[Bielu.AspNetCore.AsyncApi.Scalar.SignalR.Aspire](https://www.nuget.org/packages/Bielu.AspNetCore.AsyncApi.Scalar.SignalR.Aspire)
instead, which adds the console to a Scalar resource from the AppHost.

## Documentation

- [Scalar consoles](https://apidescriptions.bielu.pl/articles/scalar-consoles.html)
- [Full documentation](https://apidescriptions.bielu.pl/)

## Feedback & Contributing

Released under the [MIT license](https://licenses.nuget.org/MIT). Bug reports and contributions are
welcome at [the GitHub repository](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions).
