// Stand-ins for Microsoft.AspNetCore.JsonPatch.SystemTextJson's document types. The library recognizes
// JSON Patch documents by namespace and type name only (the real package is not AOT compatible, so the
// core library cannot reference it), which is exactly the contract these types satisfy.
namespace Microsoft.AspNetCore.JsonPatch.SystemTextJson;

public class JsonPatchDocument
{
}

public class JsonPatchDocument<TModel> : JsonPatchDocument
    where TModel : class
{
}
