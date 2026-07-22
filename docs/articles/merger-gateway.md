# Merger and API Gateway

Bielu.AspNetCore.AsyncApi provides tools for merging multiple AsyncAPI documents into a single "Unified API" document. This is particularly useful in microservice architectures where a central Gateway (like YARP or Ocelot) wants to present a single API specification to consumers.

## Merger Package

The `Bielu.AspNetCore.AsyncApi.Merger` package provides the core logic for merging documents.

```bash
dotnet add package Bielu.AspNetCore.AsyncApi.Merger
```

## CLI Merging

You can merge documents using the CLI tool:

```bash
dotnet asyncapi merge \
    --source ./docs/order-service.json \
    --source ./docs/inventory-service.json \
    --output ./docs/unified.json \
    --title "My Microservices API"
```

## ASP.NET Core Gateway Integration

In a Gateway project, you can use the merger to dynamically combine documents from downstream services.

```csharp
builder.Services.AddAsyncApi("gateway", options =>
{
    // The Gateway document is populated via a merger
});

// Example of a custom document transformer that merges downstream docs
public class DownstreamMergerTransformer : IAsyncApiDocumentTransformer
{
    private readonly IAsyncApiDocumentMerger _merger;
    
    public DownstreamMergerTransformer(IAsyncApiDocumentMerger merger)
    {
        _merger = merger;
    }

    public async Task TransformAsync(AsyncApiDocument document, AsyncApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        var downstreamDocs = await FetchDownstreamDocs();
        await _merger.MergeAsync(document, downstreamDocs);
    }
}
```

## .NET Aspire Example

The [Aspire Mini Shop](../../src/examples/aspire) example demonstrates a YARP API Gateway that merges AsyncAPI documents from Order Service, Inventory Service, and Notification Service using this library.
