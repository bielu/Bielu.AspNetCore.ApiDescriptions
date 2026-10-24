using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;

namespace Bielu.AspNetCore.ApiDescriptions.Mcp.Tools;

/// <summary><c>list_api_descriptions</c>: the documents the app serves, with their resource URIs.</summary>
internal sealed class ListApiDescriptionsTool() : ApiDescriptionTool(
    "list_api_descriptions",
    "List API descriptions",
    "Lists the API description documents this app serves (AsyncAPI, OpenAPI, Arazzo): their kind, name, resource URI and media type.",
    """{ "type": "object", "properties": {} }""")
{
    protected override Task<CallToolResult> InvokeAsync(ApiDescriptionCatalog catalog,
        IDictionary<string, JsonElement> arguments, CancellationToken cancellationToken)
    {
        var documents = new JsonArray(catalog.GetDocuments().Select(document => (JsonNode?)new JsonObject
        {
            ["kind"] = document.Kind,
            ["name"] = document.Name,
            ["uri"] = document.Uri,
            ["mimeType"] = document.MimeType,
        }).ToArray());
        return Task.FromResult(Json(documents));
    }
}

/// <summary><c>get_api_description</c>: one whole document.</summary>
internal sealed class GetApiDescriptionTool() : ApiDescriptionTool(
    "get_api_description",
    "Get an API description",
    "Returns one API description document this app serves, as JSON. Documents can be large: for AsyncAPI, " +
    "list_asyncapi_operations and get_asyncapi_message return just the parts asked for.",
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
    "Lists the operations of an AsyncAPI document: for each, whether the app sends or receives, on which channel and " +
    "address, and which messages.",
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
        if (await AsyncApiTools.ReadAsync(catalog, arguments) is not { } document)
        {
            return AsyncApiTools.DocumentNotFound(arguments);
        }

        return Json(AsyncApiDocumentInspector.ListOperations(document));
    }
}

/// <summary><c>get_asyncapi_message</c>: one message with its payload and headers schemas resolved.</summary>
internal sealed class GetAsyncApiMessageTool() : ApiDescriptionTool(
    "get_asyncapi_message",
    "Get an AsyncAPI message",
    "Returns one message of an AsyncAPI document, by its key under components/messages, with every schema it " +
    "references (payload, headers) copied under 'definitions', so it can be read on its own.",
    """
    {
      "type": "object",
      "properties": {
        "document": { "type": "string", "description": "The AsyncAPI document name." },
        "message": { "type": "string", "description": "The message key, as list_asyncapi_operations returns it." }
      },
      "required": ["document", "message"]
    }
    """)
{
    protected override async Task<CallToolResult> InvokeAsync(ApiDescriptionCatalog catalog,
        IDictionary<string, JsonElement> arguments, CancellationToken cancellationToken)
    {
        if (await AsyncApiTools.ReadAsync(catalog, arguments) is not { } document)
        {
            return AsyncApiTools.DocumentNotFound(arguments);
        }

        if (Argument(arguments, "message") is not { } key)
        {
            return Error("'message' is required.");
        }

        return AsyncApiDocumentInspector.GetMessage(document, key) is { } message
            ? Json(message)
            : Error($"The document has no message '{key}'. Its messages are: {string.Join(", ", AsyncApiDocumentInspector.MessageKeys(document))}.");
    }
}

internal static class AsyncApiTools
{
    internal const string AsyncApiKind = "AsyncAPI";

    public static async Task<JsonObject?> ReadAsync(ApiDescriptionCatalog catalog, IDictionary<string, JsonElement> arguments)
    {
        var name = arguments.TryGetValue("document", out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        return name is not null && catalog.Find(AsyncApiKind, name) is { } document
            ? JsonNode.Parse(await catalog.GenerateAsync(document)) as JsonObject
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
