using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Bielu.AspNetCore.ApiDescriptions.Mcp;

/// <summary>
/// Everything an AsyncAPI document says about one operation, enough to implement the other side of it: the channel and
/// its address, the servers and their protocol, the security requirements, the messages and their schemas, the protocol
/// bindings and the reply. Parts of the document are preserved as written and gathered once under
/// <c>references</c>, keyed by canonical pointer; what the document does not specify is listed under
/// <c>notSpecified</c>, so an agent can tell a gap in the contract from something left out of the response.
/// </summary>
internal static partial class AsyncApiOperationContext
{
    /// <summary>
    /// Describes <paramref name="operation"/>.
    /// </summary>
    /// <returns>
    /// The description without its references, and the references in the order they were found, for the caller to page.
    /// </returns>
    public static (JsonObject Description, IReadOnlyList<KeyValuePair<string, JsonNode>> References) Describe(
        JsonObject document, JsonObject documentInfo, ApiOperationHandle handle, AsyncApiOperation operation)
    {
        var isAsyncApi2 = AsyncApiDocumentInspector.IsAsyncApi2(document);
        var closure = new ReferenceClosure(document);
        var notSpecified = new JsonArray();
        void NotSpecified(string field, string meaning) =>
            notSpecified.Add(new JsonObject { ["field"] = field, ["meaning"] = meaning });

        // The channel first, with only what applies to this operation, so a reference to it finds the trimmed copy.
        if (operation.ChannelPointer is { } channelPointer && operation.Channel is { } channel)
        {
            var (trimmed, omitted) = Trim(channel, operation, isAsyncApi2);
            closure.Include(channelPointer, trimmed, omitted);
        }

        var servers = AsyncApiDocumentInspector.GetServers(document, operation, out var implicitServers);
        foreach (var server in servers)
        {
            closure.Add(server.Pointer);
        }

        closure.Walk(operation.Node);

        var security = Security(document, operation, servers, isAsyncApi2, closure);
        var messages = new JsonArray();
        var examples = new JsonArray();
        foreach (var message in operation.Messages)
        {
            messages.Add(Message(message, document, examples));
            if (message.Node is not null && message.Node["payload"] is null)
            {
                NotSpecified($"messages.{message.Name}.payload", "The message has no payload schema.");
            }
        }

        if (operation.Channel is null)
        {
            NotSpecified("channel", "The operation's channel could not be found.");
        }
        else if (operation.Address is null)
        {
            NotSpecified("channel.address", "The channel's address is unknown or decided at runtime.");
        }
        else
        {
            foreach (var parameter in Parameters(operation.Address))
            {
                if (operation.Channel["parameters"]?[parameter] is null)
                {
                    NotSpecified($"channel.parameters.{parameter}", $"The address uses '{{{parameter}}}', which the channel does not describe.");
                }
            }
        }

        if (servers.Count == 0)
        {
            NotSpecified("servers", "The document declares no servers, so where to connect is not part of the contract.");
        }

        if (security.Count == 0)
        {
            NotSpecified("security", "Neither the operation nor its servers declare security requirements.");
        }

        if (operation.Node["bindings"] is null)
        {
            NotSpecified("operation.bindings", "No operation bindings, so protocol-specific details of the operation (for example a SignalR hub method) are not part of the contract.");
        }

        if (operation.Channel is not null && operation.Channel["bindings"] is null)
        {
            NotSpecified("channel.bindings", "No channel bindings, so protocol-specific details of the channel (for example a SignalR hub path or a Kafka topic) are not part of the contract.");
        }

        if (operation.Messages.Count == 0)
        {
            NotSpecified("messages", "The operation lists no messages, so its payload is not part of the contract.");
        }

        if (!isAsyncApi2 && operation.Node["reply"] is null)
        {
            NotSpecified("reply", "The operation declares no reply, so any response or return value is not part of the contract.");
        }

        if (examples.Count == 0 && operation.Messages.Count > 0)
        {
            NotSpecified("examples", "The messages have no authored examples. None are generated.");
        }

        var description = new JsonObject
        {
            ["handle"] = handle.ToJson(),
            ["document"] = documentInfo,
            ["pointer"] = operation.Pointer,
            ["action"] = operation.Action,
            ["perspective"] = operation.Action == "send"
                ? "The documented application sends these messages; a client of the application would receive them."
                : "The documented application receives these messages; a client of the application would send them.",
            ["operation"] = operation.Node.DeepClone(),
            ["channel"] = operation.ChannelPointer is null ? null : new JsonObject
            {
                ["name"] = operation.ChannelKey,
                ["pointer"] = operation.ChannelPointer,
                ["address"] = operation.Address,
            },
            ["servers"] = new JsonArray(servers.Select(server => (JsonNode?)Server(server, implicitServers)).ToArray()),
            ["security"] = security,
            ["messages"] = messages,
            ["examples"] = examples,
            ["notSpecified"] = notSpecified,
        };
        if (closure.Unresolved.Count > 0)
        {
            description["unresolved"] = AsyncApiDocumentInspector.Unresolved(closure.Unresolved);
        }

        if (closure.Omitted.Count > 0)
        {
            description["omitted"] = new JsonArray(closure.Omitted
                .Select(entry => (JsonNode?)new JsonObject { ["pointer"] = entry.Key, ["note"] = entry.Value })
                .ToArray());
        }

        return (description, closure.References);
    }

    /// <summary>
    /// A copy of the channel with what does not apply to the operation left out: the channel's other messages (3.x), or
    /// its operations, which in 2.x are written inside it.
    /// </summary>
    private static (JsonObject Channel, string? Omitted) Trim(JsonObject channel, AsyncApiOperation operation, bool isAsyncApi2)
    {
        var copy = (JsonObject)channel.DeepClone();
        if (isAsyncApi2)
        {
            var removed = new[] { "subscribe", "publish" }.Where(copy.Remove).ToList();
            return (copy, removed.Count > 0
                ? $"The channel's operations ({string.Join(", ", removed)}) are left out; this operation is under 'operation'."
                : null);
        }

        if (copy["messages"] is not JsonObject messages)
        {
            return (copy, null);
        }

        var used = operation.Messages.Select(message => message.Name).ToHashSet(StringComparer.Ordinal);
        var others = messages.Select(entry => entry.Key).Where(key => !used.Contains(key)).ToList();
        foreach (var key in others)
        {
            messages.Remove(key);
        }

        return (copy, others.Count > 0
            ? $"Only the messages this operation uses are included; the channel's others ({string.Join(", ", others)}) are left out."
            : null);
    }

    private static JsonObject Server(AsyncApiServer server, bool implicitServer)
    {
        var result = new JsonObject
        {
            ["name"] = server.Name,
            ["pointer"] = server.Pointer,
            ["protocol"] = AsyncApiDocumentInspector.String(server.Node?["protocol"]),
            ["protocolVersion"] = AsyncApiDocumentInspector.String(server.Node?["protocolVersion"]),
        };

        // 3.x splits the location into host and pathname; 2.x has one url.
        if (AsyncApiDocumentInspector.String(server.Node?["host"]) is { } host)
        {
            result["host"] = host;
            result["pathname"] = AsyncApiDocumentInspector.String(server.Node?["pathname"]);
        }
        else
        {
            result["url"] = AsyncApiDocumentInspector.String(server.Node?["url"]);
        }

        if (implicitServer)
        {
            result["note"] = "The channel lists no servers, so every server of the document applies.";
        }

        return WithoutNulls(result);
    }

    /// <summary>
    /// The security schemes that apply, from the operation and from each of its servers. 3.x lists Security Scheme
    /// Objects (or references to them); 2.x lists requirement objects naming schemes under
    /// <c>components/securitySchemes</c>, with their scopes.
    /// </summary>
    private static JsonArray Security(JsonObject document, AsyncApiOperation operation, IReadOnlyList<AsyncApiServer> servers,
        bool isAsyncApi2, ReferenceClosure closure)
    {
        var result = new JsonArray();
        void AddRequirements(JsonNode? requirements, string pointer, string appliesVia)
        {
            foreach (var (requirement, index) in (requirements as JsonArray ?? []).Select((requirement, index) => (requirement, index)))
            {
                if (isAsyncApi2)
                {
                    foreach (var (name, scopes) in requirement as JsonObject ?? [])
                    {
                        var scheme = JsonPointer.Build("components", "securitySchemes", name);
                        closure.Add(scheme);
                        result.Add(Requirement(scheme, appliesVia, document, scopes));
                    }
                }
                else
                {
                    var scheme = JsonPointer.ReferenceOf(requirement) is { } reference
                        ? JsonPointer.Normalize(reference)
                        : JsonPointer.Append(pointer, "security", index.ToString(CultureInfo.InvariantCulture));
                    result.Add(Requirement(scheme, appliesVia, document, scopes: null));
                }
            }
        }

        AddRequirements(operation.Node["security"], operation.Pointer, "operation");
        foreach (var server in servers)
        {
            AddRequirements(server.Node?["security"], server.Pointer, $"server {server.Name}");
        }

        return result;
    }

    private static JsonObject Requirement(string scheme, string appliesVia, JsonObject document, JsonNode? scopes)
    {
        var (_, node, _) = JsonPointer.Follow(document, scheme);
        var requirement = new JsonObject
        {
            ["scheme"] = scheme,
            ["type"] = AsyncApiDocumentInspector.String(node?["type"]),
            ["appliesVia"] = appliesVia,
        };
        if (scopes is JsonArray { Count: > 0 })
        {
            requirement["scopes"] = scopes.DeepClone();
        }

        return requirement;
    }

    private static JsonObject Message(AsyncApiMessageReference message, JsonObject document, JsonArray examples)
    {
        var node = message.Node;
        foreach (var index in Enumerable.Range(0, (node?["examples"] as JsonArray)?.Count ?? 0))
        {
            examples.Add(new JsonObject
            {
                ["message"] = message.Pointer,
                ["pointer"] = JsonPointer.Append(message.Pointer, "examples", index.ToString(CultureInfo.InvariantCulture)),
                ["provenance"] = "authored",
            });
        }

        return WithoutNulls(new JsonObject
        {
            ["name"] = message.Name,
            ["pointer"] = message.Pointer,
            ["contentType"] = AsyncApiDocumentInspector.String(node?["contentType"]) ?? AsyncApiDocumentInspector.String(document["defaultContentType"]),
            // The schemas as written: a $ref among them is under the result's references.
            ["payload"] = node?["payload"]?.DeepClone(),
            ["headers"] = node?["headers"]?.DeepClone(),
            ["problem"] = message.Problem,
        });
    }

    /// <summary>Removes the properties that would be <see langword="null"/>, so absent fields stay absent.</summary>
    private static JsonObject WithoutNulls(JsonObject value)
    {
        foreach (var key in value.Where(entry => entry.Value is null).Select(entry => entry.Key).ToList())
        {
            value.Remove(key);
        }

        return value;
    }

    /// <summary>The <c>{parameter}</c> placeholders of a channel address.</summary>
    private static IEnumerable<string> Parameters(string address) =>
        ParameterPattern().Matches(address).Select(match => match.Groups[1].Value).Distinct(StringComparer.Ordinal);

    [GeneratedRegex(@"\{([^{}]+)\}")]
    private static partial Regex ParameterPattern();
}
