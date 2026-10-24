# Bielu.AspNetCore.AsyncApi.Avro

Describes your AsyncAPI message payloads as [Apache Avro](https://avro.apache.org/docs/1.11.1/specification/)
schemas, declared with `schemaFormat: application/vnd.apache.avro;version=1.9.0`, the way Kafka teams
using a schema registry expect them. The Avro schema is converted from the JSON Schema the generator
already produces, so it follows your .NET types without a second definition to keep in sync.

## Installation

```sh
dotnet add package Bielu.AspNetCore.AsyncApi.Avro
```

## Usage

```csharp
builder.Services.AddAsyncApi(options => options
    .AddServer("kafka", "broker:9092", "kafka")
    .AddAvroPayloads(avro => avro
        .WithNamespace("com.example.orders")    // namespace of records and enums
        .ForMessages("orderPlaced")));           // optional: only these messages
```

| JSON Schema | Avro |
| --- | --- |
| object with properties | `record` named after the schema |
| nullable / not required | union with `null` (defaulting to `null` when not required) |
| array, dictionary | `array`, `map` |
| string enum | `enum` |
| `integer` (`int32`, otherwise) | `int`, `long` |
| `number` (`float`, otherwise) | `float`, `double` |
| `uuid`, `date-time`, `date`, `time` formats | `uuid`, `timestamp-millis`, `date`, `time-millis` logical types |
| `byte` format (`byte[]`) | `bytes` |

The JSON Schema stays under `components/schemas`, and headers stay JSON Schema. The output validates
against the official AsyncAPI 2.6.0 and 3.1.0 schemas, including their Avro schema.

## Documentation

- [Avro payloads](https://apidescriptions.bielu.pl/articles/avro.html)
- [Full documentation](https://apidescriptions.bielu.pl/)

## Feedback & Contributing

Copyright (c) 2025 Arkadiusz Biel. Released under the [MIT license](https://licenses.nuget.org/MIT).
Bug reports and contributions are welcome at [the GitHub repository](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions).
