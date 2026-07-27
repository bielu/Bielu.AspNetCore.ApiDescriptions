using System.Text.Json.Nodes;
using Bielu.Overlay.Models;

namespace Bielu.Overlay.Readers;

/// <summary>
/// Walks a unified <see cref="JsonNode"/> tree — produced either directly from JSON text or converted from
/// YAML by <see cref="Bielu.Spec.Shared.YamlToJsonNodeConverter"/> — into an <see cref="OverlayDocument"/>,
/// collecting diagnostics rather than throwing. One deserializer serves both source formats.
/// </summary>
internal static class OverlayV1Deserializer
{
    private static readonly string[] DocumentFields = ["overlay", "info", "extends", "actions"];
    private static readonly string[] InfoFields = ["title", "version", "description"];
    private static readonly string[] ActionFields = ["target", "description", "update", "copy", "remove"];

    public static OverlayDocument Deserialize(JsonNode? root, ParsingContext ctx)
    {
        if (root is not JsonObject obj)
        {
            ctx.Error("/", "The root of an Overlay document must be an object.");
            return Empty();
        }

        var overlay = ReadRequiredString(obj, "overlay", "/overlay", ctx);
        var info = ReadInfo(obj["info"], ctx);
        var actions = ReadActions(obj["actions"], ctx);

        return new OverlayDocument
        {
            Overlay = overlay,
            Info = info,
            Extends = ReadOptionalString(obj, "extends", "/extends", ctx),
            Actions = actions,
            Extensions = ReadExtensions(obj, DocumentFields, "/", ctx),
        };
    }

    private static OverlayInfo ReadInfo(JsonNode? node, ParsingContext ctx)
    {
        if (node is not JsonObject obj)
        {
            ctx.Error("/info", node is null ? "info is required." : "info must be an object.");
            return new OverlayInfo { Title = string.Empty, Version = string.Empty };
        }

        return new OverlayInfo
        {
            Title = ReadRequiredString(obj, "title", "/info/title", ctx),
            Version = ReadRequiredString(obj, "version", "/info/version", ctx),
            Description = ReadOptionalString(obj, "description", "/info/description", ctx),
            Extensions = ReadExtensions(obj, InfoFields, "/info", ctx),
        };
    }

    private static List<OverlayAction> ReadActions(JsonNode? node, ParsingContext ctx)
    {
        var actions = new List<OverlayAction>();

        if (node is null)
        {
            ctx.Error("/actions", "actions is required.");
            return actions;
        }

        if (node is not JsonArray array)
        {
            ctx.Error("/actions", "actions must be an array.");
            return actions;
        }

        for (var i = 0; i < array.Count; i++)
        {
            var path = $"/actions/{i}";
            if (array[i] is not JsonObject obj)
            {
                ctx.Error(path, "Each action must be an object.");
                continue;
            }

            actions.Add(new OverlayAction
            {
                Target = ReadRequiredString(obj, "target", $"{path}/target", ctx),
                Description = ReadOptionalString(obj, "description", $"{path}/description", ctx),
                // Detached from the overlay's own tree so the action can be applied to many documents
                // without JsonNode's single-parent rule getting in the way.
                Update = obj["update"]?.DeepClone(),
                Copy = ReadOptionalString(obj, "copy", $"{path}/copy", ctx),
                Remove = ReadOptionalBool(obj, "remove", $"{path}/remove", ctx),
                Extensions = ReadExtensions(obj, ActionFields, path, ctx),
            });
        }

        return actions;
    }

    private static string ReadRequiredString(JsonObject obj, string name, string path, ParsingContext ctx)
    {
        var node = obj[name];
        if (node is null)
        {
            ctx.Error(path, $"{name} is required.");
            return string.Empty;
        }

        if (node is not JsonValue value || value.TryGetValue<string>(out _) is false)
        {
            ctx.Error(path, $"{name} must be a string.");
            return string.Empty;
        }

        return value.GetValue<string>();
    }

    private static string? ReadOptionalString(JsonObject obj, string name, string path, ParsingContext ctx)
    {
        var node = obj[name];
        if (node is null)
        {
            return null;
        }

        if (node is not JsonValue value || value.TryGetValue<string>(out var result) is false)
        {
            ctx.Error(path, $"{name} must be a string.");
            return null;
        }

        return result;
    }

    private static bool ReadOptionalBool(JsonObject obj, string name, string path, ParsingContext ctx)
    {
        var node = obj[name];
        if (node is null)
        {
            return false;
        }

        if (node is not JsonValue value || value.TryGetValue<bool>(out var result) is false)
        {
            ctx.Error(path, $"{name} must be a boolean.");
            return false;
        }

        return result;
    }

    /// <summary>Captures every field that is not a known fixed field, per spec §4.6 Specification Extensions.</summary>
    private static Dictionary<string, JsonNode?>? ReadExtensions(JsonObject obj, string[] knownFields, string path,
        ParsingContext ctx)
    {
        Dictionary<string, JsonNode?>? extensions = null;

        foreach (var (key, value) in obj)
        {
            if (knownFields.Contains(key, StringComparer.Ordinal))
            {
                continue;
            }

            if (!key.StartsWith("x-", StringComparison.Ordinal) && !ctx.Settings.IgnoreUnrecognizedFields)
            {
                ctx.Warn($"{path}/{key}",
                    $"'{key}' is not a known field; extension field names should begin with 'x-'.");
            }

            extensions ??= [];
            extensions[key] = value?.DeepClone();
        }

        return extensions;
    }

    private static OverlayDocument Empty() => new()
    {
        Overlay = string.Empty,
        Info = new OverlayInfo { Title = string.Empty, Version = string.Empty },
        Actions = [],
    };
}
