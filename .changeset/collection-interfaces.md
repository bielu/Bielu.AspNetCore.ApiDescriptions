---
"bielu-aspnetcore-asyncapi": major
---

Public collections are now typed as interfaces, so their implementation can change later without another breaking release:

- `AsyncApiOptions.OperationBindings`, `ChannelBindings`, `MessageBindings` and `ServerBindings` are `IDictionary<string, IList<…>>` (were `Dictionary<…>`).
- `AsyncApiOptions.IncludeOnlyChannels` is `ISet<string>` (was `HashSet<string>`). It still compares case-insensitively.
- `AsyncApiTypeMetadata.Members` and `AsyncApiMemberMetadata.Parameters`, `Messages`, `Operations` and `MessageExamples` are `IReadOnlyList<…>` (were `List<…>`). A custom `IAsyncApiMetadataProvider` can keep passing `List<…>` instances; code that added to these lists after construction must build them up front instead.

Code that only reads these collections, or adds through the `Add…` methods, keeps compiling unless it names the concrete type, for example `Dictionary<…> bindings = options.ChannelBindings;`.
