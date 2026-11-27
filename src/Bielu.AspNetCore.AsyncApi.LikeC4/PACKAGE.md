# Bielu.AspNetCore.AsyncApi.LikeC4

Generates a [LikeC4](https://likec4.dev) architecture model (`.c4`) from AsyncAPI documents: an element for every
service and every channel, a relationship for every operation, the service-to-service flows the channels imply, and
views of them. The diagram of who sends what to whom then follows the code instead of being drawn by hand.

## Installation

```sh
dotnet add package Bielu.AspNetCore.AsyncApi.LikeC4
```

The `dotnet asyncapi likec4` command of `Bielu.AspNetCore.AsyncApi.Cli` wraps it, for use in a build or CI job.

## Usage

```csharp
using Bielu.AspNetCore.AsyncApi.LikeC4;

var model = AsyncApiLikeC4Generator.Generate(
[
    new AsyncApiLikeC4Source("orders", File.ReadAllText("orders.asyncapi.json")),
    new AsyncApiLikeC4Source("inventory", File.ReadAllText("inventory.asyncapi.yaml")),
]);
Directory.CreateDirectory("architecture");
File.WriteAllText("architecture/asyncapi.c4", model);
```

| AsyncAPI | LikeC4 |
| --- | --- |
| document | `asyncapiService` element |
| channel (matched by address across documents) | `asyncapiChannel` element |
| `send` operation | `service -[asyncapiSend]-> channel` |
| `receive` operation | `channel -[asyncapiReceive]-> service` |
| sender and receiver of the same messages | `sender -[asyncapiFlow]-> receiver` |

Documents may be AsyncAPI 2.x or 3.x, JSON or YAML. Describe the generated elements further in a `.c4` file of your
own with `extend <id> { … }`, so the generated one can be written again on every build.

## Documentation

- [LikeC4 architecture models](https://apidescriptions.bielu.pl/articles/likec4.html)
- [Full documentation](https://apidescriptions.bielu.pl/)

## Feedback & Contributing

Copyright (c) 2025 Arkadiusz Biel. Released under the [MIT license](https://licenses.nuget.org/MIT).
Bug reports and contributions are welcome at [the GitHub repository](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions).
