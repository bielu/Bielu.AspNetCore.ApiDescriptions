using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;

namespace Bielu.AspNetCore.ApiDescriptions.Mcp.Tools;

/// <summary>
/// <c>find_api_operations</c>: searches the operations of every document the app serves, and returns for each match a
/// handle <c>get_api_operation</c> takes. Only AsyncAPI documents are searched so far, and the result says which
/// documents were not.
/// </summary>
internal sealed class FindApiOperationsTool() : ApiDescriptionTool(
    "find_api_operations",
    "Find API operations",
    "Searches the operations of this app's API descriptions by words in their ids, titles, summaries, tags, channel " +
    "names and addresses, message names and protocol bindings (for example a SignalR hub method name). Returns the " +
    "best matches first, each with a handle to pass to get_api_operation for its complete contract. Without a query, " +
    "lists every operation that passes the filters. 'send' and 'receive' are what the documented app does: a client " +
    "receives what the app sends. AsyncAPI documents are searched; the result lists any that are not.",
    """
    {
      "type": "object",
      "properties": {
        "query": { "type": "string", "description": "Words to look for, for example 'send chat message'. Optional." },
        "kind": { "type": "string", "description": "Only documents of this kind, for example AsyncAPI." },
        "document": { "type": "string", "description": "Only this document, by name." },
        "action": { "type": "string", "enum": ["send", "receive"], "description": "Only operations in which the app sends, or receives." },
        "protocol": { "type": "string", "description": "Only operations reachable over this protocol, for example signalr, kafka or mqtt." },
        "limit": { "type": "integer", "minimum": 1, "maximum": 50, "default": 10, "description": "The most matches to return." },
        "cursor": { "type": "string", "description": "The nextCursor of a previous call, for the next matches." }
      }
    }
    """,
    $$"""
    {
      "type": "object",
      "properties": {
        "matches": {
          "type": "array",
          "items": {
            "type": "object",
            "properties": {
              "handle": {{ApiOperationHandle.Schema}},
              "action": { "type": "string", "enum": ["send", "receive"] },
              "channel": { "type": "string" },
              "address": { "type": "string" },
              "summary": { "type": "string" },
              "messages": { "type": "array", "items": { "type": "string" } },
              "protocols": { "type": "array", "items": { "type": "string" } },
              "matchedTerms": { "type": "integer" },
              "score": { "type": "integer" }
            },
            "required": ["handle", "action", "messages", "protocols"]
          }
        },
        "total": { "type": "integer", "description": "How many operations match in all." },
        "nextCursor": { "type": "string", "description": "Pass as 'cursor' for the next matches; absent on the last page." },
        "notSearched": {
          "type": "array",
          "items": {
            "type": "object",
            "properties": { "kind": { "type": "string" }, "name": { "type": "string" }, "reason": { "type": "string" } },
            "required": ["kind", "name", "reason"]
          }
        }
      },
      "required": ["matches", "total", "notSearched"]
    }
    """)
{
    private const string CursorPurpose = "find";

    protected override async Task<CallToolResult> InvokeAsync(ApiDescriptionCatalog catalog,
        IDictionary<string, JsonElement> arguments, CancellationToken cancellationToken)
    {
        var action = Argument(arguments, "action");
        if (action is not (null or "send" or "receive"))
        {
            return Error("'action' is 'send' or 'receive'.");
        }

        var offset = 0;
        if (Argument(arguments, "cursor") is { } cursor && !Cursor.TryRead(cursor, CursorPurpose, out _, out offset))
        {
            return Error("'cursor' is not one find_api_operations returned. Call again without it.");
        }

        var kind = Argument(arguments, "kind");
        var name = Argument(arguments, "document");
        var protocol = Argument(arguments, "protocol");
        var limit = Integer(arguments, "limit", fallback: 10, minimum: 1, maximum: 50);
        var terms = AsyncApiOperationSearch.Terms(Argument(arguments, "query"));

        var documents = catalog.GetDocuments()
            .Where(document => kind is null || string.Equals(document.Kind, kind, StringComparison.OrdinalIgnoreCase))
            .Where(document => name is null || string.Equals(document.Name, name, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (documents.Count == 0)
        {
            return Error($"No document matches{(kind is null ? "" : $" kind '{kind}'")}{(name is null ? "" : $" name '{name}'")}. " +
                         "Call list_api_descriptions for the documents this app serves.");
        }

        var notSearched = new JsonArray();
        var matches = new List<(JsonObject Match, int Matched, int Score)>();
        foreach (var document in documents)
        {
            if (!string.Equals(document.Kind, AsyncApiTools.AsyncApiKind, StringComparison.Ordinal))
            {
                notSearched.Add(NotSearched(document, $"Operations of {document.Kind} documents cannot be searched yet; get_api_description returns the whole document."));
                continue;
            }

            GeneratedApiDescription generated;
            try
            {
                generated = await catalog.LoadAsync(document);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                notSearched.Add(NotSearched(document, $"The document could not be generated: {exception.Message}"));
                continue;
            }

            if (generated.Json is not { } json)
            {
                notSearched.Add(NotSearched(document, "The document is not a JSON object."));
                continue;
            }

            foreach (var operation in AsyncApiDocumentInspector.GetOperations(json))
            {
                var protocols = AsyncApiOperationSearch.Protocols(json, operation);
                if ((action is not null && operation.Action != action)
                    || (protocol is not null && !protocols.Contains(protocol, StringComparer.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var (matched, score) = AsyncApiOperationSearch.Match(json, operation, terms);
                if (terms.Count > 0 && matched == 0)
                {
                    continue;
                }

                matches.Add((Match(document, operation, protocols, terms.Count > 0 ? (matched, score) : null), matched, score));
            }
        }

        // A stable sort, so equally good matches keep document order and pages do not overlap.
        var ranked = matches.OrderByDescending(match => match.Matched).ThenByDescending(match => match.Score).ToList();
        var result = new JsonObject
        {
            ["matches"] = new JsonArray(ranked.Skip(offset).Take(limit).Select(match => (JsonNode?)match.Match).ToArray()),
            ["total"] = ranked.Count,
        };
        if (offset + limit < ranked.Count)
        {
            result["nextCursor"] = Cursor.Create(CursorPurpose, string.Empty, offset + limit);
        }

        result["notSearched"] = notSearched;
        return Structured(result);
    }

    private static JsonObject Match(ApiDescriptionDocument document, AsyncApiOperation operation, IReadOnlyList<string> protocols,
        (int Matched, int Score)? relevance)
    {
        var match = new JsonObject
        {
            ["handle"] = new ApiOperationHandle(document.Kind, document.Name, operation.Id).ToJson(),
            ["action"] = operation.Action,
        };
        if (operation.ChannelKey is not null)
        {
            match["channel"] = operation.ChannelKey;
        }

        if (operation.Address is not null)
        {
            match["address"] = operation.Address;
        }

        if ((AsyncApiDocumentInspector.String(operation.Node["summary"]) ?? AsyncApiDocumentInspector.String(operation.Node["description"])) is { } summary)
        {
            match["summary"] = summary;
        }

        match["messages"] = new JsonArray(operation.Messages.Select(message => (JsonNode?)message.Name).ToArray());
        match["protocols"] = new JsonArray(protocols.Select(protocol => (JsonNode?)protocol).ToArray());
        if (relevance is var (matched, score))
        {
            match["matchedTerms"] = matched;
            match["score"] = score;
        }

        return match;
    }

    private static JsonObject NotSearched(ApiDescriptionDocument document, string reason) => new()
    {
        ["kind"] = document.Kind,
        ["name"] = document.Name,
        ["reason"] = reason,
    };
}
