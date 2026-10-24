---
"bielu-aspnetcore-asyncapi": major
---

The transformer contexts no longer expose MVC ApiExplorer types. These members came from `Microsoft.AspNetCore.OpenApi`, which builds operations from HTTP endpoints. AsyncAPI documents here are built from attributes and integrations, so the members were empty or never set:

- `AsyncApiOperationTransformerContext.Description` (`ApiDescription?`) is removed. It was always `null`.
- `AsyncApiJsonSchemaTransformerContext.ParameterDescription` (`ApiParameterDescription?`) is removed. It was always `null` unless a transformer passed one to `GetOrCreateSchemaAsync` itself.
- The `parameterDescription` parameter is removed from `GetOrCreateSchemaAsync` on all three contexts. It is now `GetOrCreateSchemaAsync(Type type, CancellationToken cancellationToken = default)`, so a call passing `cancellationToken:` by name keeps compiling.
- `AsyncApiDocumentTransformerContext.DescriptionGroups` is removed. It listed the app's HTTP ApiExplorer groups. Resolve `IApiDescriptionGroupCollectionProvider` from `context.ApplicationServices` if a document transformer needs them.
