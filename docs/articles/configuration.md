# Configuration

You can configure AsyncAPI generation using the fluent API in `AddAsyncApi`.

## Basic Options

```csharp
builder.Services.AddAsyncApi(options =>
{
    // Set document version (default is AsyncApi3_0)
    options.AsyncApiVersion = AsyncApiVersion.AsyncApi3_0;
    
    // Configure document info
    options.WithInfo("My API", "1.0.0")
           .WithDescription("Detailed description")
           .WithLicense("MIT", "https://mit-license.org");
           
    // Default content type for messages
    options.WithDefaultContentType("application/json");
});
```

## Servers

Define the servers where your API is available.

```csharp
options.AddServer("production", "api.example.com", "amqp", server =>
{
    server.Description = "Production AMQP broker";
    server.ProtocolVersion = "0.9.1";
});
```

## XML Comments

To use your C# XML documentation as descriptions in the AsyncAPI document, enable `IncludeXmlComments`.

```csharp
options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "MyApi.xml"));
```

Or for an assembly:

```csharp
options.IncludeXmlComments(typeof(Startup).Assembly);
```

> **Note:** Ensure `<GenerateDocumentationFile>true</GenerateDocumentationFile>` is set in your `.csproj`.

## Transformers

Transformers allow you to modify the generated document or schemas.

### Document Transformers

Implement `IAsyncApiDocumentTransformer` to modify the final document.

```csharp
public class MyDocumentTransformer : IAsyncApiDocumentTransformer
{
    public Task TransformAsync(AsyncApiDocument document, AsyncApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Info.Description += " (Modified by transformer)";
        return Task.CompletedTask;
    }
}

// Register it
options.AddDocumentTransformer<MyDocumentTransformer>();
```

### Schema Transformers

Implement `IAsyncApiSchemaTransformer` to modify JSON schemas.

```csharp
options.AddSchemaTransformer<MySchemaTransformer>();
```

## JSON Schema Options

You can customize how JSON schemas are generated, for example, to use different property naming policies.

```csharp
options.AsyncApiJsonSchemaJsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
};
```
