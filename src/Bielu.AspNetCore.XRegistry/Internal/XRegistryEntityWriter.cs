using System.Text.Json;

namespace Bielu.AspNetCore.XRegistry.Internal;

/// <summary>
/// Serializes the registry's entities in the API view of xRegistry 1.0-rc4: absolute <c>self</c> URLs, collection URLs
/// and counts, and nested collections only where the <c>?inline</c> flag asks for them.
/// </summary>
/// <remarks>
/// The registry is read-only and built from the app's documents, so every entity has <c>epoch</c> 1, the time the
/// registry was mapped as <c>createdat</c> and <c>modifiedat</c>, and exactly one Version, <c>1</c>.
/// </remarks>
internal sealed class XRegistryEntityWriter(string baseUrl, DateTimeOffset createdAt, InlineSelection inline)
{
    internal const string MessageGroups = "messagegroups";
    internal const string Messages = "messages";
    internal const string SchemaGroups = "schemagroups";
    internal const string Schemas = "schemas";
    internal const string VersionId = "1";
    private const string Details = "$details";

    /// <summary>The <c>createdat</c> and <c>modifiedat</c> of every entity, in RFC 3339.</summary>
    public string Timestamp { get; } = createdAt.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");

    public string Url(string xid) => baseUrl + xid;

    public static string GroupXid(string groups, string groupId) => $"/{groups}/{groupId}";

    public static string ResourceXid(string groups, string groupId, string resources, string resourceId) =>
        $"/{groups}/{groupId}/{resources}/{resourceId}";

    public void WriteRegistry(Utf8JsonWriter writer, RegistrySnapshot snapshot, XRegistryOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("specversion", XRegistryDefaults.SpecVersion);
        writer.WriteString("registryid", options.RegistryId);
        writer.WriteString("self", Url("/"));
        writer.WriteString("xid", "/");
        writer.WriteNumber("epoch", 1);
        WriteOptional(writer, "name", options.Name);
        WriteOptional(writer, "description", options.Description);
        WriteTimestamps(writer);

        if (inline.Includes("capabilities"))
        {
            writer.WritePropertyName("capabilities");
            XRegistryStaticDocuments.WriteCapabilities(writer);
        }

        if (inline.Includes("model"))
        {
            writer.WritePropertyName("model");
            XRegistryStaticDocuments.WriteModel(writer);
        }

        WriteCollection(writer, MessageGroups, "/" + MessageGroups, snapshot.Groups,
            group => WriteMessageGroup(writer, group, MessageGroups), group => group.Id);
        WriteCollection(writer, SchemaGroups, "/" + SchemaGroups, snapshot.Groups,
            group => WriteSchemaGroup(writer, group, SchemaGroups), group => group.Id);
        writer.WriteEndObject();
    }

    /// <summary>Writes a message group. <paramref name="path"/> is its inline path, empty when it is the target.</summary>
    public void WriteMessageGroup(Utf8JsonWriter writer, RegistryGroup group, string path)
    {
        var xid = GroupXid(MessageGroups, group.Id);
        WriteGroupStart(writer, "messagegroupid", group, xid);
        WriteCollection(writer, Join(path, Messages), xid + "/" + Messages, group.Messages,
            message => WriteMessage(writer, group, message, Join(path, Messages)), message => message.Id);
        writer.WriteEndObject();
    }

    public void WriteSchemaGroup(Utf8JsonWriter writer, RegistryGroup group, string path)
    {
        var xid = GroupXid(SchemaGroups, group.Id);
        WriteGroupStart(writer, "schemagroupid", group, xid);
        WriteCollection(writer, Join(path, Schemas), xid + "/" + Schemas, group.Schemas,
            schema => WriteSchema(writer, group, schema, Join(path, Schemas)), schema => schema.Id);
        writer.WriteEndObject();
    }

    /// <summary>Writes a message Resource, with its default Version's attributes.</summary>
    public void WriteMessage(Utf8JsonWriter writer, RegistryGroup group, MessageDefinition message, string path)
    {
        var xid = ResourceXid(MessageGroups, group.Id, Messages, message.Id);
        writer.WriteStartObject();
        writer.WriteString("messageid", message.Id);
        writer.WriteString("versionid", VersionId);
        writer.WriteString("self", Url(xid));
        writer.WriteString("xid", xid);
        WriteMessageVersionAttributes(writer, group, message);
        WriteResourceEnd(writer, path, xid, "messageid", message.Id, hasDocument: false,
            _ => WriteMessageVersion(writer, group, message));
    }

    public void WriteMessageVersion(Utf8JsonWriter writer, RegistryGroup group, MessageDefinition message)
    {
        var xid = ResourceXid(MessageGroups, group.Id, Messages, message.Id) + "/versions/" + VersionId;
        writer.WriteStartObject();
        writer.WriteString("messageid", message.Id);
        writer.WriteString("versionid", VersionId);
        writer.WriteString("self", Url(xid));
        writer.WriteString("xid", xid);
        WriteMessageVersionAttributes(writer, group, message);
        writer.WriteEndObject();
    }

    /// <summary>Writes a schema Resource, with its default Version's attributes.</summary>
    public void WriteSchema(Utf8JsonWriter writer, RegistryGroup group, SchemaDefinition schema, string path)
    {
        var xid = ResourceXid(SchemaGroups, group.Id, Schemas, schema.Id);
        writer.WriteStartObject();
        writer.WriteString("schemaid", schema.Id);
        writer.WriteString("versionid", VersionId);
        writer.WriteString("self", Url(xid) + Details);
        writer.WriteString("xid", xid);
        WriteSchemaVersionAttributes(writer, schema, Join(path, "schema"));
        WriteResourceEnd(writer, path, xid, "schemaid", schema.Id, hasDocument: true,
            version => WriteSchemaVersion(writer, group, schema, version));
    }

    public void WriteSchemaVersion(Utf8JsonWriter writer, RegistryGroup group, SchemaDefinition schema, string path)
    {
        var xid = ResourceXid(SchemaGroups, group.Id, Schemas, schema.Id) + "/versions/" + VersionId;
        writer.WriteStartObject();
        writer.WriteString("schemaid", schema.Id);
        writer.WriteString("versionid", VersionId);
        writer.WriteString("self", Url(xid) + Details);
        writer.WriteString("xid", xid);
        WriteSchemaVersionAttributes(writer, schema, Join(path, "schema"));
        writer.WriteEndObject();
    }

    /// <summary>Writes the <c>meta</c> entity of a Resource.</summary>
    public void WriteMeta(Utf8JsonWriter writer, string resourceIdAttribute, string resourceId, string resourceXid, bool hasDocument)
    {
        writer.WriteStartObject();
        writer.WriteString(resourceIdAttribute, resourceId);
        writer.WriteString("self", Url(resourceXid + "/meta"));
        writer.WriteString("xid", resourceXid + "/meta");
        writer.WriteNumber("epoch", 1);
        WriteTimestamps(writer);
        writer.WriteBoolean("readonly", true);
        writer.WriteString("defaultversionid", VersionId);
        writer.WriteString("defaultversionurl", Url(resourceXid + "/versions/" + VersionId) + (hasDocument ? Details : string.Empty));
        writer.WriteBoolean("defaultversionsticky", false);
        writer.WriteEndObject();
    }

    /// <summary>Writes a collection of <paramref name="entities"/> as the map the collection's URL returns.</summary>
    public static void WriteMap<T>(Utf8JsonWriter writer, IEnumerable<T> entities, Func<T, string> id, Action<T> write)
    {
        writer.WriteStartObject();
        foreach (var entity in entities)
        {
            writer.WritePropertyName(id(entity));
            write(entity);
        }

        writer.WriteEndObject();
    }

    private void WriteMessageVersionAttributes(Utf8JsonWriter writer, RegistryGroup group, MessageDefinition message)
    {
        writer.WriteNumber("epoch", 1);
        WriteOptional(writer, "name", message.Name);
        writer.WriteBoolean("isdefault", true);
        WriteOptional(writer, "description", message.Description);
        WriteTimestamps(writer);
        writer.WriteString("ancestorid", VersionId);

        if (message.Schema is { } schema)
        {
            var schemaXid = ResourceXid(SchemaGroups, group.Id, Schemas, schema.Id);
            writer.WriteString("dataschemaformat", schema.Format);
            writer.WriteString("dataschemauri", Url(schemaXid));
            writer.WriteString("dataschemaxid", schemaXid);
        }

        WriteOptional(writer, "datacontenttype", message.DataContentType);
    }

    private void WriteSchemaVersionAttributes(Utf8JsonWriter writer, SchemaDefinition schema, string documentPath)
    {
        writer.WriteNumber("epoch", 1);
        WriteOptional(writer, "name", schema.Name);
        writer.WriteBoolean("isdefault", true);
        WriteTimestamps(writer);
        writer.WriteString("ancestorid", VersionId);
        writer.WriteString("contenttype", schema.ContentType);
        writer.WriteString("format", schema.Format);

        if (inline.Includes(documentPath))
        {
            if (schema.JsonDocument is { } json)
            {
                writer.WritePropertyName("schema");
                json.WriteTo(writer);
            }
            else
            {
                writer.WriteBase64String("schemabase64", schema.Document);
            }
        }
    }

    private void WriteResourceEnd(Utf8JsonWriter writer, string path, string xid, string resourceIdAttribute,
        string resourceId, bool hasDocument, Action<string> writeVersion)
    {
        writer.WriteString("metaurl", Url(xid + "/meta"));
        if (inline.Includes(Join(path, "meta")))
        {
            writer.WritePropertyName("meta");
            WriteMeta(writer, resourceIdAttribute, resourceId, xid, hasDocument);
        }

        var versionsPath = Join(path, "versions");
        if (inline.Includes(versionsPath))
        {
            writer.WritePropertyName("versions");
            writer.WriteStartObject();
            writer.WritePropertyName(VersionId);
            writeVersion(versionsPath);
            writer.WriteEndObject();
        }

        writer.WriteString("versionsurl", Url(xid + "/versions"));
        writer.WriteNumber("versionscount", 1);
        writer.WriteEndObject();
    }

    private void WriteGroupStart(Utf8JsonWriter writer, string idAttribute, RegistryGroup group, string xid)
    {
        writer.WriteStartObject();
        writer.WriteString(idAttribute, group.Id);
        writer.WriteString("self", Url(xid));
        writer.WriteString("xid", xid);
        writer.WriteNumber("epoch", 1);
        WriteOptional(writer, "name", group.Name);
        WriteOptional(writer, "description", group.Description);
        WriteTimestamps(writer);
    }

    private void WriteCollection<T>(Utf8JsonWriter writer, string path, string xid, IReadOnlyCollection<T> entities,
        Action<T> write, Func<T, string> id)
    {
        var name = path[(path.LastIndexOf('.') + 1)..];
        if (inline.Includes(path))
        {
            writer.WritePropertyName(name);
            WriteMap(writer, entities, id, write);
        }

        writer.WriteString(name + "url", Url(xid));
        writer.WriteNumber(name + "count", entities.Count);
    }

    private void WriteTimestamps(Utf8JsonWriter writer)
    {
        writer.WriteString("createdat", Timestamp);
        writer.WriteString("modifiedat", Timestamp);
    }

    private static void WriteOptional(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is not null)
        {
            writer.WriteString(name, value);
        }
    }

    private static string Join(string path, string name) => path.Length == 0 ? name : $"{path}.{name}";
}
