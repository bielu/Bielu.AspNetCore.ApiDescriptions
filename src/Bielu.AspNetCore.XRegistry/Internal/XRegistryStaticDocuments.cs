using System.Text.Json;

namespace Bielu.AspNetCore.XRegistry.Internal;

/// <summary>The registry's capabilities and model, which do not depend on its content.</summary>
internal static class XRegistryStaticDocuments
{
    private const string MessageModel = "https://xregistry.io/xreg/domains/message/specs/model.json";
    private const string SchemaModel = "https://xregistry.io/xreg/domains/schema/specs/model.json";

    /// <summary>Writes the capabilities: read-only, the <c>inline</c> flag, one Version per Resource.</summary>
    public static void WriteCapabilities(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteStartObject("available");
        foreach (var available in new[] { "capabilities", "entities", "model" })
        {
            writer.WriteStartObject(available);
            writer.WriteBoolean("mutable", false);
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
        WriteStrings(writer, "flags", "inline");
        WriteStrings(writer, "mutable");
        writer.WriteBoolean("pagination", false);
        writer.WriteBoolean("shortself", false);
        WriteStrings(writer, "specversions", XRegistryDefaults.SpecVersion);
        WriteStrings(writer, "versionmodes", "manual");
        writer.WriteEndObject();
    }

    /// <summary>
    /// Writes the model: the message and schema Group types of the xRegistry message and schema specifications, with
    /// the attributes this registry fills.
    /// </summary>
    public static void WriteModel(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteStartObject("groups");

        writer.WriteStartObject(XRegistryEntityWriter.MessageGroups);
        writer.WriteString("singular", "messagegroup");
        writer.WriteString("modelversion", XRegistryDefaults.SpecVersion);
        writer.WriteString("modelcompatiblewith", MessageModel);
        writer.WriteStartObject("resources");
        writer.WriteStartObject(XRegistryEntityWriter.Messages);
        writer.WriteString("singular", "message");
        writer.WriteNumber("maxversions", 1);
        writer.WriteBoolean("hasdocument", false);
        writer.WriteString("modelversion", XRegistryDefaults.SpecVersion);
        writer.WriteString("modelcompatiblewith", MessageModel);
        writer.WriteStartObject("attributes");
        WriteAttribute(writer, "dataschemaformat", "string");
        WriteAttribute(writer, "dataschemauri", "uri");
        WriteAttribute(writer, "dataschemaxid", "xid", target: "/schemagroups/schemas[/versions]");
        WriteAttribute(writer, "datacontenttype", "string");
        writer.WriteEndObject();
        writer.WriteEndObject();
        writer.WriteEndObject();
        writer.WriteEndObject();

        writer.WriteStartObject(XRegistryEntityWriter.SchemaGroups);
        writer.WriteString("singular", "schemagroup");
        writer.WriteString("modelversion", XRegistryDefaults.SpecVersion);
        writer.WriteString("modelcompatiblewith", SchemaModel);
        writer.WriteStartObject("resources");
        writer.WriteStartObject(XRegistryEntityWriter.Schemas);
        writer.WriteString("singular", "schema");
        writer.WriteNumber("maxversions", 1);
        writer.WriteBoolean("hasdocument", true);
        writer.WriteString("modelversion", XRegistryDefaults.SpecVersion);
        writer.WriteString("modelcompatiblewith", SchemaModel);
        writer.WriteStartObject("attributes");
        WriteAttribute(writer, "format", "string", required: true);
        writer.WriteEndObject();
        writer.WriteEndObject();
        writer.WriteEndObject();
        writer.WriteEndObject();

        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static void WriteAttribute(Utf8JsonWriter writer, string name, string type, bool required = false, string? target = null)
    {
        writer.WriteStartObject(name);
        writer.WriteString("name", name);
        writer.WriteString("type", type);
        if (target is not null)
        {
            writer.WriteString("target", target);
        }

        if (required)
        {
            writer.WriteBoolean("required", true);
        }

        writer.WriteEndObject();
    }

    private static void WriteStrings(Utf8JsonWriter writer, string name, params string[] values)
    {
        writer.WriteStartArray(name);
        foreach (var value in values)
        {
            writer.WriteStringValue(value);
        }

        writer.WriteEndArray();
    }
}
