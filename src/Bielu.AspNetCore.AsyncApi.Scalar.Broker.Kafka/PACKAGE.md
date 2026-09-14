# Bielu.AspNetCore.AsyncApi.Scalar.Broker.Kafka

The **Kafka driver** for the interactive Scalar broker console. Lets the console publish to and tail
the Kafka topics your `kafka` AsyncAPI bindings describe.

Install it alongside the console package, which owns the bridge and the endpoints:

```shell
dotnet add package Bielu.AspNetCore.AsyncApi.Scalar.Broker
dotnet add package Bielu.AspNetCore.AsyncApi.Scalar.Broker.Kafka
```

## Usage

```csharp
builder.Services.AddScalarBrokerBridge(options =>
{
    options.AddKafkaConnection("orders", "localhost:9092");

    options.AddKafkaConnection("secure", "broker:9093", kafka =>
    {
        kafka.ConfigureProducer = config =>
        {
            config.SecurityProtocol = SecurityProtocol.SaslSsl;
            config.SaslMechanism = SaslMechanism.ScramSha512;
            config.SaslUsername = "console";
            config.SaslPassword = builder.Configuration["Kafka:Password"];
        };
    });
});

app.MapScalarBrokerAssets().RequireAuthorization("BrokerConsole");
```

See the [`Bielu.AspNetCore.AsyncApi.Scalar.Broker`](https://www.nuget.org/packages/Bielu.AspNetCore.AsyncApi.Scalar.Broker)
package for how to wire the console into Scalar and, importantly, **how to secure the bridge** — it
can publish to your cluster.

## Behaviour worth knowing

**Tailing has no side effects on your cluster.** Each tail builds a consumer with a throwaway group
id (`bielu-scalar-broker-{guid}`), starts at `AutoOffsetReset.Latest`, and never commits. Opening a
console therefore cannot move a real consumer group's committed offsets, and cannot replay a backlog
into someone's browser. `ConfigureConsumer` can override these, but setting a fixed `GroupId` or
re-enabling auto-commit gives that guarantee up.

**Connections are lazy.** The producer is built on the first publish, so a cluster that is
unreachable at startup does not stop your application from starting — the console reports the error
instead.

**Credentials never reach the browser.** The console is told a connection's name, protocol and a
display endpoint only, and any `user:password@` in `bootstrapServers` is redacted out of it.

Non-UTF-8 message headers are shown as `<n bytes>` rather than decoded into mojibake.

## Documentation

Full documentation: <https://apidescriptions.bielu.pl/>
