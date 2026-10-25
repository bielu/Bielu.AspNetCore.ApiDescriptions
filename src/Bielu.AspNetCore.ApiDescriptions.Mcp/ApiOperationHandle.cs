using System.Text.Json.Nodes;

namespace Bielu.AspNetCore.ApiDescriptions.Mcp;

/// <summary>
/// Identifies one operation unambiguously across every document the app serves. <c>find_api_operations</c> returns it,
/// and <c>get_api_operation</c> takes its three fields as arguments.
/// </summary>
/// <param name="Kind">The document kind, for example <c>AsyncAPI</c>.</param>
/// <param name="Document">The document name.</param>
/// <param name="Operation">The operation's id within the document.</param>
internal sealed record ApiOperationHandle(string Kind, string Document, string Operation)
{
    public JsonObject ToJson() => new()
    {
        ["kind"] = Kind,
        ["document"] = Document,
        ["operation"] = Operation,
    };

    /// <summary>The JSON Schema of <see cref="ToJson"/>, for tools' output schemas.</summary>
    public const string Schema = """
        {
          "type": "object",
          "description": "Identifies the operation. Pass its fields to get_api_operation.",
          "properties": {
            "kind": { "type": "string" },
            "document": { "type": "string" },
            "operation": { "type": "string" }
          },
          "required": ["kind", "document", "operation"]
        }
        """;
}
