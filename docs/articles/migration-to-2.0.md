# Migrating to 2.0

2.0 upgrades the AsyncAPI object model to ByteBard.AsyncAPI.NET 4.0 and uses the major version to clean up API that came over from `Microsoft.AspNetCore.OpenApi` but never applied to AsyncAPI. Most apps only need the namespace change and the operation attribute renames. The [changelog](https://github.com/bielu/Bielu.AspNetCore.ApiDescriptions/blob/main/CHANGELOG.md) lists every change.

## Checklist

1. Update every `Bielu.*` package to 2.0. They are versioned together, so mixing 1.x and 2.0 packages isn't supported.
2. Replace `using Bielu.AspNetCore.AsyncApi.Attributes.Attributes;` with `using Bielu.AspNetCore.AsyncApi.Attributes;`.
3. Replace `[PublishOperation]` with **`[ReceiveOperation]`** and `[SubscribeOperation]` with **`[SendOperation]`**. Note the inversion, explained [below](#publishsubscribe-become-receivesend).
4. If anything depends on generated operation IDs, set `OperationId` explicitly or update the references.
5. Fix the compile errors from the [removed and changed members](#removed-and-changed-members).
6. If something other than `AddOpenApi()` describes your minimal APIs, such as Swashbuckle, call `AddEndpointsApiExplorer()` yourself.

## Attributes namespace

The attribute types moved from `Bielu.AspNetCore.AsyncApi.Attributes.Attributes` to `Bielu.AspNetCore.AsyncApi.Attributes`, dropping the doubled segment. The types and their members are unchanged, so this is a find-and-replace:

```diff
-using Bielu.AspNetCore.AsyncApi.Attributes.Attributes;
+using Bielu.AspNetCore.AsyncApi.Attributes;
```

## Publish/Subscribe become Receive/Send

Operations are now named from the application's point of view, as in AsyncAPI 3: `[SendOperation]` produces `action: send` and `[ReceiveOperation]` produces `action: receive`. `OperationType` is now `Send`/`Receive` instead of `Publish`/`Subscribe`.

The 2.x names described the operation from the **client's** side, so the mapping is inverted:

| 1.x | 2.0 | Meaning |
|-----|-----|---------|
| `[PublishOperation]` | `[ReceiveOperation]` | The app receives messages that clients publish |
| `[SubscribeOperation]` | `[SendOperation]` | The app sends messages that clients subscribe to |
| `OperationType.Publish` | `OperationType.Receive` | |
| `OperationType.Subscribe` | `OperationType.Send` | |

> [!WARNING]
> A plain find-and-replace of `Publish` → `Send` reverses every operation. Map them as in the table above.

```diff
 [AsyncApi]
 public class LightMeasuredHandler
 {
     [Channel("light.measured")]
-    [PublishOperation(typeof(LightMeasuredEvent))]
+    [ReceiveOperation(typeof(LightMeasuredEvent))]
     public void Handle(LightMeasuredEvent message) { }
 }
```

`[PublishOperation]` and `[SubscribeOperation]` still compile, with an `[Obsolete]` warning, and produce the same operations as in 1.x. You can migrate one handler at a time.

### Default operation IDs change

An operation without an explicit `OperationId` gets the ID `{Type}_{Method}_{OperationType}`. The suffix follows the new names, so `_Publish` becomes `_Receive` and `_Subscribe` becomes `_Send`. Anything that refers to an operation by ID breaks, such as an Arazzo workflow's `operationId`, a generated client, or a test snapshot. Either update those references, or pin the old ID:

```csharp
[ReceiveOperation(typeof(LightMeasuredEvent), OperationId = "LightMeasuredHandler_Handle_Publish")]
```

### Custom operation attributes and the source generator

The source generator now emits attributes derived from `[SendOperation]` or `[ReceiveOperation]`. Before, it dropped them silently. Such an attribute must be `internal` or `public`. If it isn't, for example a `private` nested class, the new **BASYNC011** warning reports that its operation is left out of the generated document. See [Native AOT](native-aot.md).

## Removed and changed members

| 1.x | 2.0 |
|-----|-----|
| `AsyncApiOptions.ShouldInclude` | Removed. Generation never called it. Use `AddIncludedChannel(...)` / `IncludeOnlyChannels`, or a document transformer |
| `AsyncApiOperationTransformerContext.Description` | Removed. It was always `null` |
| `AsyncApiJsonSchemaTransformerContext.ParameterDescription` | Removed |
| `AsyncApiDocumentTransformerContext.DescriptionGroups` | Removed. Resolve `IApiDescriptionGroupCollectionProvider` from `context.ApplicationServices` if you need it |
| `GetOrCreateSchemaAsync(type, parameterDescription, cancellationToken)` | `GetOrCreateSchemaAsync(type, cancellationToken)` on all three contexts |
| `AsyncApiGeneratorConstants.DefaultAsyncApiVersion` | `AsyncApiGeneratorConstants.DefaultInfoVersion`. Same value (`"1.0.0"`, the default `info.version`) |

A call to `GetOrCreateSchemaAsync` that passes `cancellationToken:` by name keeps compiling.

### Collections are typed as interfaces

| Member | 1.x | 2.0 |
|--------|-----|-----|
| `AsyncApiOptions.OperationBindings`, `ChannelBindings`, `MessageBindings`, `ServerBindings` | `Dictionary<string, List<…>>` | `IDictionary<string, IList<…>>` |
| `AsyncApiOptions.IncludeOnlyChannels` | `HashSet<string>` | `ISet<string>` (still case-insensitive) |
| `AsyncApiTypeMetadata.Members`, `AsyncApiMemberMetadata.Parameters`, `Messages`, `Operations`, `MessageExamples` | `List<…>` | `IReadOnlyList<…>` |

Code that reads these collections or adds through the `Add…` methods is unaffected. Code that names the concrete type, such as `Dictionary<…> bindings = options.ChannelBindings;`, needs `var` or the interface type. A custom `IAsyncApiMetadataProvider` can keep passing `List<…>` instances, but it must build the metadata lists up front instead of adding to them after construction.

## The endpoint ApiExplorer isn't registered anymore

`AddAsyncApi()` and `AddAsyncApiForApiVersions()` no longer call `AddEndpointsApiExplorer()`, since document generation doesn't use it. `AddOpenApi()` registers what it needs, and so does Asp.Versioning's `AddApiExplorer()`, which `AddAsyncApiForApiVersions()` requires, so most apps notice nothing. If something else relied on it, such as Swashbuckle's `AddSwaggerGen()` describing minimal APIs, register it yourself. This applies to apps that also use controllers too: `AddControllers()` registers the ApiExplorer for controllers only, so without the call their minimal APIs drop out of the Swagger document.

```csharp
builder.Services.AddEndpointsApiExplorer();
```

## API versioning

`AddAsyncApiForApiVersions()` serves one document per API version, with that version's `info` and deprecation flag. The documents were never filtered by version: in 1.x the `ShouldInclude` filter it set was never called. This is unchanged in 2.0, so each document lists every channel. To limit a version's document, include its channels in the `configure` callback:

```csharp
builder.Services.AddAsyncApiForApiVersions((options, version) =>
{
    if (version.ApiVersion.MajorVersion == 2)
    {
        options.AddIncludedChannel("orders.v2");
    }
});
```

## ByteBard.AsyncAPI.NET 4.0

The AsyncAPI object model (`AsyncApiDocument`, `AsyncApiChannel`, the binding types, …) comes from ByteBard.AsyncAPI.NET, which moves from 3.0 to 4.0. Its types appear in transformers, options and bindings, so code that uses them directly may need updating. See the [ByteBard.AsyncAPI.NET repository](https://github.com/ByteBardOrg/AsyncAPI.NET) for its changes. Code that only uses this library's attributes and `AsyncApiOptions` methods doesn't need any changes.

ByteBard 4.0 also fixes three binding bugs in 3.0, so generated documents change:

- WebSockets channel bindings use the `ws` key.
- Kafka message bindings write `schemaIdLocation` instead of `SchemaIdLocation`.
- SQS queue bindings write `fifoQueue` even when it is `false`, since the schema requires it.

In AsyncAPI 2.x documents, channel servers are now listed by name as the spec requires, and message traits without headers no longer get an empty `headers` schema.
