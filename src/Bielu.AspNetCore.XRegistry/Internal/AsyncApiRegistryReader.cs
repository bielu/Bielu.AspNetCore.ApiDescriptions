using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bielu.AspNetCore.ApiDescriptions;
using Microsoft.Extensions.DependencyInjection;

namespace Bielu.AspNetCore.XRegistry.Internal;

/// <summary>
/// Reads the app's AsyncAPI documents into a <see cref="RegistrySnapshot"/>. It reads them as JSON, the way build-time
/// generation writes them, so serialized-document transformers such as overlays have already been applied.
/// </summary>
internal static class AsyncApiRegistryReader
{
    /// <summary>The <see cref="IBuildTimeDocumentSource.Kind"/> of the AsyncAPI documents.</summary>
    internal const string AsyncApiKind = "AsyncAPI";

    internal const string JsonSchemaFormat = "JsonSchema/draft-07";
    private const string JsonSchemaDialect = "http://json-schema.org/draft-07/schema#";
    private const string SchemaReferencePrefix = "#/components/schemas/";
    private const string DefinitionsReferencePrefix = "#/definitions/";

    private static readonly JsonSerializerOptions DocumentWriterOptions = new() { WriteIndented = true };

    public static async Task<RegistrySnapshot> ReadAsync(IServiceProvider services, XRegistryOptions options)
    {
        var groups = new List<RegistryGroup>();
        var groupIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var source in services.GetServices<IBuildTimeDocumentSource>()
                     .Where(source => string.Equals(source.Kind, AsyncApiKind, StringComparison.Ordinal)))
        {
            foreach (var document in source.GetDocuments()
                         .Where(document => options.IsIncluded(document.Name))
                         .OrderBy(document => document.Name, StringComparer.Ordinal))
            {
                await using var writer = new StringWriter();
                await source.GenerateAsync(document.Name, writer);
                groups.Add(ReadGroup(UniqueId(document.Name, groupIds), writer.ToString()));
            }
        }

        return new RegistrySnapshot(groups);
    }

    /// <summary>Reads the messages under <c>components/messages</c> of one AsyncAPI document.</summary>
    internal static RegistryGroup ReadGroup(string groupId, string asyncApiJson)
    {
        var root = JsonNode.Parse(asyncApiJson) as JsonObject ?? new JsonObject();
        var isAsyncApi2 = root["asyncapi"]?.GetValue<string>().StartsWith("2.", StringComparison.Ordinal) == true;
        var defaultContentType = String(root["defaultContentType"]);
        var componentSchemas = root["components"]?["schemas"] as JsonObject;

        var messages = new List<MessageDefinition>();
        var messageIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (key, node) in root["components"]?["messages"] as JsonObject ?? [])
        {
            if (node is not JsonObject message || message.ContainsKey("$ref"))
            {
                continue;
            }

            var id = UniqueId(key, messageIds);
            var name = String(message["title"]) ?? String(message["name"]);
            var schema = ReadSchema(id, name, message, isAsyncApi2, componentSchemas);
            messages.Add(new MessageDefinition(
                id,
                name,
                String(message["description"]) ?? String(message["summary"]),
                String(message["contentType"]) ?? defaultContentType,
                schema));
        }

        var info = root["info"];
        return new RegistryGroup(
            groupId,
            String(info?["title"]),
            String(info?["description"]),
            messages,
            messages.Select(message => message.Schema).OfType<SchemaDefinition>().ToList());
    }

    private static SchemaDefinition? ReadSchema(string id, string? name, JsonObject message, bool isAsyncApi2,
        JsonObject? componentSchemas)
    {
        // 2.x puts schemaFormat on the message; 3.x wraps a non-default payload in { schemaFormat, schema }.
        var payload = message["payload"];
        var schemaFormat = isAsyncApi2 ? String(message["schemaFormat"]) : null;
        if (!isAsyncApi2 && payload is JsonObject multiFormat && multiFormat.ContainsKey("schemaFormat") && multiFormat.ContainsKey("schema"))
        {
            schemaFormat = String(multiFormat["schemaFormat"]);
            payload = multiFormat["schema"];
        }

        if (payload is null)
        {
            return null;
        }

        if (schemaFormat is not null && schemaFormat.Contains("vnd.google.protobuf", StringComparison.OrdinalIgnoreCase))
        {
            var source = String(payload) ?? payload.ToJsonString();
            return new SchemaDefinition(id, name, "Protobuf/" + FormatVersion(schemaFormat, "3"), "text/plain; charset=utf-8",
                Encoding.UTF8.GetBytes(source), JsonDocument: null);
        }

        if (schemaFormat is not null && schemaFormat.Contains("vnd.apache.avro", StringComparison.OrdinalIgnoreCase))
        {
            return Json(id, name, "Avro/" + FormatVersion(schemaFormat, "1.9.0"), "application/json", payload.DeepClone());
        }

        if (schemaFormat is null || IsJsonSchema(schemaFormat))
        {
            return Json(id, name, JsonSchemaFormat, "application/schema+json", Standalone(payload, componentSchemas));
        }

        return Json(id, name, schemaFormat, "application/json", payload.DeepClone());
    }

    private static SchemaDefinition Json(string id, string? name, string format, string contentType, JsonNode document) =>
        new(id, name, format, contentType, Encoding.UTF8.GetBytes(document.ToJsonString(DocumentWriterOptions)), document);

    /// <summary>
    /// Makes a payload schema a standalone JSON Schema document: every schema it references under
    /// <c>components/schemas</c>, transitively, is copied under <c>definitions</c> and the references point there.
    /// </summary>
    internal static JsonNode Standalone(JsonNode payload, JsonObject? componentSchemas)
    {
        var definitions = new JsonObject();

        // A payload that is only a reference becomes the schema it references, so the document's root is the schema.
        var root = payload is JsonObject { Count: 1 } onlyReference && ReferencedKey(onlyReference) is { } rootKey
                   && componentSchemas?[rootKey] is JsonObject referenced
            ? referenced.DeepClone()
            : payload.DeepClone();

        Rewrite(root, definitions, componentSchemas);
        if (root is not JsonObject rootObject)
        {
            return root;
        }

        var document = new JsonObject { ["$schema"] = JsonSchemaDialect };
        foreach (var (key, value) in rootObject.ToList())
        {
            rootObject.Remove(key);
            document[key] = value;
        }

        if (definitions.Count > 0)
        {
            document["definitions"] = definitions;
        }

        return document;
    }

    private static void Rewrite(JsonNode? node, JsonObject definitions, JsonObject? componentSchemas)
    {
        switch (node)
        {
            case JsonObject schema:
                if (ReferencedKey(schema) is { } key)
                {
                    schema["$ref"] = DefinitionsReferencePrefix + key;
                    if (!definitions.ContainsKey(key) && componentSchemas?[key] is { } target)
                    {
                        var copy = target.DeepClone();
                        definitions[key] = copy;
                        Rewrite(copy, definitions, componentSchemas);
                    }
                }

                foreach (var (_, child) in schema.ToList())
                {
                    Rewrite(child, definitions, componentSchemas);
                }

                break;
            case JsonArray array:
                foreach (var child in array)
                {
                    Rewrite(child, definitions, componentSchemas);
                }

                break;
        }
    }

    private static string? ReferencedKey(JsonObject schema) =>
        schema["$ref"] is JsonValue value && value.TryGetValue<string>(out var reference)
                                          && reference.StartsWith(SchemaReferencePrefix, StringComparison.Ordinal)
            ? reference[SchemaReferencePrefix.Length..]
            : null;

    private static bool IsJsonSchema(string schemaFormat) =>
        schemaFormat.Contains("schema+json", StringComparison.OrdinalIgnoreCase)
        || schemaFormat.Contains("schema+yaml", StringComparison.OrdinalIgnoreCase)
        || schemaFormat.Contains("vnd.aai.asyncapi", StringComparison.OrdinalIgnoreCase);

    private static string FormatVersion(string schemaFormat, string fallback)
    {
        const string versionParameter = "version=";
        var index = schemaFormat.IndexOf(versionParameter, StringComparison.OrdinalIgnoreCase);
        return index < 0 ? fallback : schemaFormat[(index + versionParameter.Length)..].Split(';')[0].Trim();
    }

    private static string? String(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) && text.Length > 0 ? text : null;

    /// <summary>
    /// Makes <paramref name="value"/> a valid xRegistry id, <c>[a-zA-Z0-9_][a-zA-Z0-9_.\-~@]{0,127}</c>, unique among
    /// <paramref name="taken"/>.
    /// </summary>
    internal static string UniqueId(string value, HashSet<string> taken)
    {
        var builder = new StringBuilder();
        foreach (var character in value)
        {
            builder.Append(char.IsAsciiLetterOrDigit(character) || character is '_' or '.' or '-' or '~' or '@' ? character : '_');
        }

        if (builder.Length == 0 || !(char.IsAsciiLetterOrDigit(builder[0]) || builder[0] == '_'))
        {
            builder.Insert(0, '_');
        }

        var id = builder.Length > 128 ? builder.ToString(0, 128) : builder.ToString();
        var candidate = id;
        for (var suffix = 2; !taken.Add(candidate); suffix++)
        {
            var tail = $"_{suffix}";
            candidate = (id.Length + tail.Length > 128 ? id[..(128 - tail.Length)] : id) + tail;
        }

        return candidate;
    }
}
