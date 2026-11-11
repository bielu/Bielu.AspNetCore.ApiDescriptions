# Union types

On .NET 11, a [C# 15 union](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/union) can be a
message payload, or a property inside one. Nothing needs to be registered. Unions need the `net11.0` target: the
`union` keyword is C# 15, and System.Text.Json's union support is new in .NET 11.

```csharp
public record Cat(string Name, string Coat);
public record Dog(string Name, string Breed);

public union Pet(Cat, Dog);
public union Code(int, string);

public class PetBus
{
    [Channel("pets")]
    [SendOperation(typeof(Pet))]
    public void Publish(Pet pet) { }
}
```

System.Text.Json writes a union as its active case, with no wrapper and no discriminator. A `Pet` holding a `Dog` goes
on the wire as `{"name":"Rex","breed":"Labrador"}`. The schema therefore says "one of these shapes" with an `anyOf`
that has one entry per case, in declaration order:

```json
"pet": {
  "type": "object",
  "anyOf": [
    { "properties": { "name": { "type": "string" }, "coat": { "type": "string" } }, "required": ["name", "coat"] },
    { "properties": { "name": { "type": "string" }, "breed": { "type": "string" } }, "required": ["name", "breed"] }
  ]
},
"code": {
  "anyOf": [
    { "type": "integer", "format": "int32" },
    { "type": "string" }
  ]
}
```

This is also how ASP.NET Core 11's OpenAPI generator describes unions, so an app's AsyncAPI and OpenAPI documents agree
on the same type. It works in both AsyncAPI 3.0 and 2.x documents.

- **Nullable unions.** A `Pet?` property adds `"null"` to `type` and keeps the cases.
- **Custom unions.** A type marked `[System.Runtime.CompilerServices.Union]` that follows the union pattern (one
  public constructor per case and a `Value` property) is described the same way. Library union types can opt in this
  way.
- **Schema transformers.** These run on each case schema with that case's `JsonTypeInfo`, as they do for the derived
  types of a polymorphic base. A transformer that matches `context.JsonTypeInfo.Type == typeof(Dog)` therefore changes
  only the `Dog` entry.
- **Message examples.** An `IAsyncApiMessageExampleProvider` can return a union value. The example contains the
  active case, just as it goes on the wire.

## Unions or closed hierarchies

A union has no discriminator, so a consumer that only sees the JSON can't always tell two object cases apart. `Cat`
and `Dog` above share `name`; a reader has to look for `coat` or `breed`. When you design a new contract and own all
the types, a C# 15 closed hierarchy with inferred polymorphism is usually the better fit. Every case then carries
`$type`:

```csharp
[JsonPolymorphic(InferClosedTypePolymorphism = true)]
public closed record PaymentEvent(string PaymentId);

public record PaymentAuthorized(string PaymentId, decimal Amount) : PaymentEvent(PaymentId);
public record PaymentFailed(string PaymentId, string Reason) : PaymentEvent(PaymentId);
```

Its schema is an `anyOf` in which every case pins `$type` with a `const`, just like a `[JsonDerivedType]` hierarchy.
Choose a union for
an existing discriminator-free contract, for primitive cases, or for cases that can't share a base class. Microsoft's
[unions and closed hierarchies in ASP.NET Core](https://devblogs.microsoft.com/dotnet/unions-and-closed-hierarchies-in-aspnetcore/)
covers the trade-off in more detail.

## Limitations

- **SignalR.** Unions need SignalR's JSON hub protocol. The MessagePack and Newtonsoft.Json hub protocols can't
  serialize them.
- **Deserializing ambiguous cases.** This is a consumer concern, not a schema one. Cases that start with the same JSON
  token (two object cases, for example) need a `[JsonUnion(TypeClassifier = ...)]` classifier to deserialize. The
  generated schema doesn't change.
- **Native AOT.** A union payload must be in your `JsonSerializerContext` like any other payload type. The source
  generator supports unions.
