# Native AOT Support

`Bielu.AspNetCore.AsyncApi` supports [Native AOT](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/) (Ahead-of-Time compilation). Native AOT provides faster startup times, reduced memory usage, and smaller deployment sizes.

Because Native AOT disables reflection-based discovery of services and types at runtime, you must use the provided Source Generator and explicitly configure JSON metadata for your message types.

## Prerequisites

Native AOT support is available starting from version 1.0.0. Ensure your project is targeting `.NET 8.0` or higher and has `<IsAotCompatible>true</IsAotCompatible>` in the `.csproj`.

## Configuration

In a Native AOT application, the library cannot scan your assemblies for attributes at runtime. Instead, a Source Generator finds all `[AsyncApi]` and `[Channel]` attributes during compilation and generates metadata classes.

### 1. Register Generated Metadata

In your `Program.cs`, you must call `AddAsyncApiGeneratedMetadata()` after `AddAsyncApi()`. This tells the library to use the source-generated metadata instead of attempting to use reflection.

```csharp
var builder = WebApplication.CreateBuilder(args);

// Standard AsyncAPI registration
builder.Services.AddAsyncApi();

// Register source-generated metadata (Required for AOT)
builder.Services.AddAsyncApiGeneratedMetadata();

var app = builder.Build();
app.MapAsyncApi();
app.Run();
```

### 2. Configure JSON Serialization

Native AOT requires all types that are serialized or used for schema generation to be known at compile time. You must define a `JsonSerializerContext` that includes all your message types and register it in the `TypeInfoResolverChain`.

```csharp
using System.Text.Json.Serialization;

// Configure JSON options for AOT
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, MyProjectJsonContext.Default);
});

// Define the context with all message types
[JsonSerializable(typeof(MyMessage))]
[JsonSerializable(typeof(AnotherMessage))]
internal partial class MyProjectJsonContext : JsonSerializerContext { }
```

> [!IMPORTANT]
> If you forget to include a message type in your `JsonSerializerContext`, you will receive an `InvalidOperationException` at runtime when the library attempts to generate the AsyncAPI schema for that type.

### 3. Runtime directives (supplied for you)

Nothing to do here — this section documents what the package does on your behalf, and how to change
it if you need to.

`Bielu.AspNetCore.AsyncApi` ships an `rd.xml` runtime directives file and applies it automatically
when `PublishAot` is `true`, including when you reference the package indirectly through
`Bielu.AspNetCore.AsyncApi.Merger`, `.Versioning`, a protocol extension or a Scalar console.

It exists because the underlying `ByteBard.AsyncAPI` serializer resolves enums from their display
names through `Enum.GetValues()`, which calls `Array.CreateInstance` — constructing an array type at
runtime. ILC does not emit metadata for a type it cannot see being used, and that call is invisible
to its analysis, so without the directives the application publishes and starts normally and then
throws on the first document request:

```text
System.NotSupportedException: 'ByteBard.AsyncAPI.Models.ReferenceType[]' is missing native code or metadata.
```

`TrimmerRootAssembly` does not solve this — it governs which assemblies survive trimming, not which
types get reflection metadata.

To supply your own set instead:

```xml
<PropertyGroup>
  <BieluAsyncApiIncludeRuntimeDirectives>false</BieluAsyncApiIncludeRuntimeDirectives>
</PropertyGroup>
```

## Using Scalar with Native AOT

Scalar UI is also compatible with Native AOT. When mapping the Scalar UI, ensure you reference the correct document path:

```csharp
app.MapScalarApiReference(options =>
{
    options.AddAsyncApiDocument("v1", "My API", "/asyncapi/v1.json");
});
```

## Known Limitations

- **Custom Providers**: If you implement custom `IAsyncApiMetadataProvider` or `IAsyncApiSchemaTransformer`, ensure they are also AOT-compatible and do not rely on reflection to discover types.
- **Dynamic Types**: Using `dynamic` or non-annotated `object` types for messages is not supported in Native AOT.
- **Publish warnings from `ByteBard.AsyncAPI`**: the dependency is not trim/AOT-annotated, so an AOT publish reports `IL2104`/`IL3053` from that assembly. They come from outside this project and do not affect the generated document — the `aot-verification` CI job asserts the AOT build and the reflection-based build serve documents that are equal once parsed as JSON. The comparison is deliberately structural rather than byte-for-byte, since property ordering is not part of the contract.
