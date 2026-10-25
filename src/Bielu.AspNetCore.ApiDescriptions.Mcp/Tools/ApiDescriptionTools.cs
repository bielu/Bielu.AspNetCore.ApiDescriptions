using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;

namespace Bielu.AspNetCore.ApiDescriptions.Mcp.Tools;

/// <summary>
/// <c>list_api_descriptions</c>: the documents the app serves, with what an agent needs to choose one without
/// downloading each: title, description, specification version, and the tools that can inspect it.
/// </summary>
internal sealed class ListApiDescriptionsTool() : ApiDescriptionTool(
    "list_api_descriptions",
    "List API descriptions",
    "Lists the API description documents this app serves (AsyncAPI, OpenAPI, Arazzo): for each, its kind, name, " +
    "resource URI, media type, title, description, specification version, and the tools that can inspect it. " +
    "Start here, or with find_api_operations to look for an operation across every document.",
    """{ "type": "object", "properties": {} }""",
    """
    {
      "type": "object",
      "properties": {
        "documents": {
          "type": "array",
          "items": {
            "type": "object",
            "properties": {
              "kind": { "type": "string" },
              "name": { "type": "string" },
              "uri": { "type": "string" },
              "mimeType": { "type": "string" },
              "title": { "type": "string" },
              "description": { "type": "string" },
              "specificationVersion": { "type": "string" },
              "fingerprint": { "type": "string" },
              "capabilities": { "type": "array", "items": { "type": "string" }, "description": "The tools that can inspect this document." },
              "error": { "type": "string", "description": "Why the document could not be generated." }
            },
            "required": ["kind", "name", "uri", "mimeType", "capabilities"]
          }
        }
      },
      "required": ["documents"]
    }
    """)
{
    /// <summary>Descriptions longer than this are cut short: the list is for choosing a document, not reading it.</summary>
    private const int MaxDescriptionLength = 500;

    protected override async Task<CallToolResult> InvokeAsync(ApiDescriptionCatalog catalog,
        IDictionary<string, JsonElement> arguments, CancellationToken cancellationToken)
    {
        var documents = new JsonArray();
        foreach (var document in catalog.GetDocuments())
        {
            var entry = new JsonObject
            {
                ["kind"] = document.Kind,
                ["name"] = document.Name,
                ["uri"] = document.Uri,
                ["mimeType"] = document.MimeType,
            };
            try
            {
                var generated = await catalog.LoadAsync(document);
                Set(entry, "title", AsyncApiDocumentInspector.String(generated.Json?["info"]?["title"]));
                Set(entry, "description", Shorten(AsyncApiDocumentInspector.String(generated.Json?["info"]?["description"])));
                Set(entry, "specificationVersion", generated.SpecificationVersion);
                entry["fingerprint"] = generated.Fingerprint;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // One broken document should not hide the others.
                entry["error"] = $"The document could not be generated: {exception.Message}";
            }

            entry["capabilities"] = new JsonArray(Capabilities(document).Select(tool => (JsonNode?)tool).ToArray());
            documents.Add(entry);
        }

        return Structured(new JsonObject { ["documents"] = documents });
    }

    /// <summary>The tools that can inspect a document of the kind; only AsyncAPI documents can be inspected so far.</summary>
    internal static IEnumerable<string> Capabilities(ApiDescriptionDocument document) =>
        string.Equals(document.Kind, AsyncApiTools.AsyncApiKind, StringComparison.OrdinalIgnoreCase)
            ? ["get_api_description", "find_api_operations", "get_api_operation", "list_asyncapi_operations", "get_asyncapi_message"]
            : ["get_api_description"];

    private static void Set(JsonObject entry, string name, string? value)
    {
        if (value is not null)
        {
            entry[name] = value;
        }
    }

    private static string? Shorten(string? text) =>
        text is { Length: > MaxDescriptionLength } ? text[..MaxDescriptionLength] + "…" : text;
}

/// <summary><c>get_api_description</c>: one whole document.</summary>
internal sealed class GetApiDescriptionTool() : ApiDescriptionTool(
    "get_api_description",
    "Get an API description",
    "Returns one API description document this app serves, as JSON. Documents can be large: for AsyncAPI, " +
    "find_api_operations and get_api_operation return just the parts that matter for one operation.",
    """
    {
      "type": "object",
      "properties": {
        "kind": { "type": "string", "description": "The kind of document: AsyncAPI, OpenAPI or Arazzo." },
        "name": { "type": "string", "description": "The document name, as list_api_descriptions returns it." }
      },
      "required": ["kind", "name"]
    }
    """)
{
    protected override async Task<CallToolResult> InvokeAsync(ApiDescriptionCatalog catalog,
        IDictionary<string, JsonElement> arguments, CancellationToken cancellationToken)
    {
        if (Argument(arguments, "kind") is not { } kind || Argument(arguments, "name") is not { } name)
        {
            return Error("Both 'kind' and 'name' are required.");
        }

        return catalog.Find(kind, name) is { } document
            ? Text(await catalog.GenerateAsync(document))
            : Error($"No {kind} document is called '{name}'. Call list_api_descriptions for the documents this app serves.");
    }
}

/// <summary><c>list_asyncapi_operations</c>: what an AsyncAPI document says the app sends and receives.</summary>
internal sealed class ListAsyncApiOperationsTool() : ApiDescriptionTool(
    "list_asyncapi_operations",
    "List AsyncAPI operations",
    "Lists every operation of one AsyncAPI document: for each, whether the app sends or receives, on which channel " +
    "and address, and which messages (by name and canonical pointer, either of which get_asyncapi_message accepts). " +
    "To look for an operation, find_api_operations is shorter.",
    """
    {
      "type": "object",
      "properties": { "document": { "type": "string", "description": "The AsyncAPI document name." } },
      "required": ["document"]
    }
    """)
{
    protected override async Task<CallToolResult> InvokeAsync(ApiDescriptionCatalog catalog,
        IDictionary<string, JsonElement> arguments, CancellationToken cancellationToken)
    {
        if (await AsyncApiTools.ReadAsync(catalog, arguments) is not { Json: { } document })
        {
            return AsyncApiTools.DocumentNotFound(arguments);
        }

        return Json(AsyncApiDocumentInspector.ListOperations(document));
    }
}

/// <summary><c>get_asyncapi_message</c>: one message with every part of the document it references.</summary>
internal sealed class GetAsyncApiMessageTool() : ApiDescriptionTool(
    "get_asyncapi_message",
    "Get an AsyncAPI message",
    "Returns one message of an AsyncAPI document with every part of the document it references (payload and headers " +
    "schemas, traits, …) under 'references', keyed by canonical pointer, so each $ref can be looked up there as written.",
    """
    {
      "type": "object",
      "properties": {
        "document": { "type": "string", "description": "The AsyncAPI document name." },
        "message": {
          "type": "string",
          "description": "The message: its canonical pointer (the 'ref' list_asyncapi_operations returns), its key under components/messages, or the name an operation or channel knows it by."
        }
      },
      "required": ["document", "message"]
    }
    """)
{
    protected override async Task<CallToolResult> InvokeAsync(ApiDescriptionCatalog catalog,
        IDictionary<string, JsonElement> arguments, CancellationToken cancellationToken)
    {
        if (await AsyncApiTools.ReadAsync(catalog, arguments) is not { Json: { } document })
        {
            return AsyncApiTools.DocumentNotFound(arguments);
        }

        if (Argument(arguments, "message") is not { } key)
        {
            return Error("'message' is required.");
        }

        var (message, error) = AsyncApiDocumentInspector.GetMessage(document, key);
        return message is not null ? Json(message) : Error(error!);
    }
}

internal static class AsyncApiTools
{
    internal const string AsyncApiKind = "AsyncAPI";

    /// <summary>Generates the AsyncAPI document the <c>document</c> argument names, or returns <see langword="null"/>.</summary>
    public static async Task<GeneratedApiDescription?> ReadAsync(ApiDescriptionCatalog catalog, IDictionary<string, JsonElement> arguments)
    {
        var name = arguments.TryGetValue("document", out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        return name is not null && catalog.Find(AsyncApiKind, name) is { } document
            ? await catalog.LoadAsync(document)
            : null;
    }

    public static CallToolResult DocumentNotFound(IDictionary<string, JsonElement> arguments) => new()
    {
        IsError = true,
        Content =
        [
            new TextContentBlock
            {
                Text = arguments.TryGetValue("document", out var value) && value.ValueKind == JsonValueKind.String
                    ? $"No AsyncAPI document is called '{value.GetString()}'. Call list_api_descriptions for the documents this app serves."
                    : "'document' is required.",
            },
        ],
    };
}
