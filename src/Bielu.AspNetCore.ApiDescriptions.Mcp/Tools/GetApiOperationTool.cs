using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;

namespace Bielu.AspNetCore.ApiDescriptions.Mcp.Tools;

/// <summary>
/// <c>get_api_operation</c>: the complete documented contract of one operation, from a handle
/// <c>find_api_operations</c> returned. The parts of the document it depends on come under <c>references</c>; when they
/// exceed <see cref="ReferenceBudget"/>, the rest follows page by page through <c>nextCursor</c>.
/// </summary>
internal sealed class GetApiOperationTool() : ApiDescriptionTool(
    "get_api_operation",
    "Get an API operation",
    "Returns the complete documented contract of one operation, from a handle find_api_operations returned: the " +
    "operation as written, its channel and address, the servers (host and protocol), the security requirements, the " +
    "messages with their payload and headers schemas, protocol bindings (for example the SignalR hub path and method), " +
    "the reply, and authored examples. Every part of the document these refer to is under 'references', keyed by " +
    "canonical pointer, so each $ref can be looked up there as written. 'notSpecified' lists what the document does " +
    "not say, which is a gap in the contract rather than in this response; 'omitted' lists what was left out because " +
    "it does not apply to this operation. 'action' is what the documented app does: a client receives what it sends. " +
    "When 'nextCursor' is present, call again with it for the remaining references.",
    """
    {
      "type": "object",
      "properties": {
        "kind": { "type": "string", "description": "The handle's kind, for example AsyncAPI." },
        "document": { "type": "string", "description": "The handle's document." },
        "operation": { "type": "string", "description": "The handle's operation." },
        "cursor": { "type": "string", "description": "The nextCursor of a previous call, for the next page of references." }
      },
      "required": ["kind", "document", "operation"]
    }
    """,
    $$"""
    {
      "type": "object",
      "properties": {
        "handle": {{ApiOperationHandle.Schema}},
        "document": {
          "type": "object",
          "description": "The document quoted, and the fingerprint of the exact content.",
          "properties": {
            "kind": { "type": "string" },
            "name": { "type": "string" },
            "uri": { "type": "string" },
            "specificationVersion": { "type": ["string", "null"] },
            "fingerprint": { "type": "string" }
          },
          "required": ["kind", "name", "uri", "fingerprint"]
        },
        "pointer": { "type": "string", "description": "Where the operation is in the document." },
        "action": { "type": "string", "enum": ["send", "receive"] },
        "perspective": { "type": "string" },
        "operation": { "type": "object", "description": "The operation as written in the document." },
        "channel": { "type": ["object", "null"] },
        "servers": { "type": "array", "items": { "type": "object" } },
        "security": { "type": "array", "items": { "type": "object" } },
        "messages": { "type": "array", "items": { "type": "object" } },
        "examples": { "type": "array", "items": { "type": "object" } },
        "notSpecified": {
          "type": "array",
          "items": {
            "type": "object",
            "properties": { "field": { "type": "string" }, "meaning": { "type": "string" } },
            "required": ["field", "meaning"]
          }
        },
        "unresolved": { "type": "array", "items": { "type": "object" } },
        "omitted": { "type": "array", "items": { "type": "object" } },
        "references": {
          "type": "object",
          "description": "The parts of the document referred to, keyed by canonical pointer.",
          "additionalProperties": true
        },
        "nextCursor": { "type": "string", "description": "Pass as 'cursor' for the remaining references." }
      },
      "required": ["handle", "document", "references"]
    }
    """)
{
    /// <summary>
    /// Roughly how many characters of references one response carries. A schema larger than this on its own is still
    /// returned whole, alone on its page.
    /// </summary>
    internal const int ReferenceBudget = 48 * 1024;

    private const string CursorPurpose = "references";

    protected override async Task<CallToolResult> InvokeAsync(ApiDescriptionCatalog catalog,
        IDictionary<string, JsonElement> arguments, CancellationToken cancellationToken)
    {
        if (Argument(arguments, "kind") is not { } kind || Argument(arguments, "document") is not { } name
            || Argument(arguments, "operation") is not { } id)
        {
            return Error("'kind', 'document' and 'operation' are required: pass the fields of a handle find_api_operations returned.");
        }

        if (!string.Equals(kind, AsyncApiTools.AsyncApiKind, StringComparison.OrdinalIgnoreCase))
        {
            return Error($"Operations of {kind} documents cannot be inspected yet; get_api_description returns the whole document.");
        }

        if (catalog.Find(kind, name) is not { } source)
        {
            return Error($"No {kind} document is called '{name}'. Call list_api_descriptions for the documents this app serves.");
        }

        var generated = await catalog.LoadAsync(source);
        if (generated.Json is not { } document)
        {
            return Error($"The {kind} document '{name}' is not a JSON object.");
        }

        if (AsyncApiDocumentInspector.FindOperation(document, id) is not { } operation)
        {
            return Error($"The document '{source.Name}' has no operation '{id}'. Call find_api_operations to look for one.");
        }

        var start = 0;
        if (Argument(arguments, "cursor") is { } cursor)
        {
            if (!Cursor.TryRead(cursor, CursorPurpose, out var fingerprint, out start))
            {
                return Error("'cursor' is not one get_api_operation returned. Call again without it.");
            }

            if (fingerprint != generated.Fingerprint)
            {
                return Error("The document has changed since the cursor was returned. Call again without it.");
            }
        }

        var handle = new ApiOperationHandle(source.Kind, source.Name, operation.Id);
        var (description, references) = AsyncApiOperationContext.Describe(document, generated.Describe(), handle, operation);

        // Later pages carry only the references, under the same handle and fingerprint.
        var result = start == 0
            ? description
            : new JsonObject { ["handle"] = handle.ToJson(), ["document"] = generated.Describe() };

        var (page, next) = Page(references, start);
        result["references"] = page;
        if (next is { } offset)
        {
            result["nextCursor"] = Cursor.Create(CursorPurpose, generated.Fingerprint, offset);
        }

        return Structured(result);
    }

    /// <summary>
    /// The references from <paramref name="start"/> that fit in <see cref="ReferenceBudget"/> (at least one), and where
    /// the next page starts, if any remain.
    /// </summary>
    internal static (JsonObject Page, int? Next) Page(IReadOnlyList<KeyValuePair<string, JsonNode>> references, int start,
        int budget = ReferenceBudget)
    {
        var page = new JsonObject();
        var size = 0;
        var index = start;
        for (; index < references.Count; index++)
        {
            var (pointer, node) = references[index];
            var length = pointer.Length + node.ToJsonString().Length;
            if (page.Count > 0 && size + length > budget)
            {
                break;
            }

            page[pointer] = node.DeepClone();
            size += length;
        }

        return (page, index < references.Count ? index : null);
    }
}
