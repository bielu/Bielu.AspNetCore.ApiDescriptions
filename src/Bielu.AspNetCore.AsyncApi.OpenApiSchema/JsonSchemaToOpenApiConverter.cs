using System.Text.Json.Nodes;
using ByteBard.AsyncAPI.Models;
using ByteBard.AsyncAPI.Writers;

namespace Bielu.AspNetCore.AsyncApi.OpenApiSchema;

/// <summary>
/// Converts the JSON Schema of a message payload into an OpenAPI 3.0 Schema Object, writing referenced component
/// schemas inline so the payload reads in one dialect.
/// </summary>
/// <remarks>
/// OpenAPI 3.0 has a single <c>type</c> with <c>nullable</c>, boolean <c>exclusiveMinimum</c>/<c>exclusiveMaximum</c>
/// flags, one <c>example</c>, no <c>const</c>, and a Discriminator Object. Keywords it has no counterpart for are
/// dropped rather than written, since the OpenAPI 3.0 Schema Object allows no other keywords.
/// </remarks>
internal sealed class JsonSchemaToOpenApiConverter(AsyncApiDocument document)
{
    private const string SchemaReferencePrefix = "#/components/schemas/";

    // The component schemas being written, so that a recursive reference stays a $ref instead of looping.
    private readonly HashSet<string> _inlining = new(StringComparer.Ordinal);

    /// <summary>Converts a payload schema.</summary>
    public JsonObject Convert(AsyncApiJsonSchema schema) => ConvertSchema(schema);

    private JsonObject ConvertSchema(AsyncApiJsonSchema schema)
    {
        if (schema is AsyncApiJsonSchemaReference { Reference.Reference: { } reference })
        {
            return ConvertReference(reference);
        }

        if (schema is FalseApiSchema)
        {
            // OpenAPI 3.0 has no boolean schemas: "not anything" says the same.
            return new JsonObject { ["not"] = new JsonObject() };
        }

        var result = new JsonObject();
        AddString(result, "title", schema.Title);
        AddString(result, "description", schema.Description);

        var nullable = schema.Nullable || schema.Type?.HasFlag(SchemaType.Null) == true
                       || schema.Enum?.Any(value => value?.GetNode() is null) == true;
        var types = SchemaTypes(schema.Type);
        if (types is [var single])
        {
            result["type"] = single;
        }
        else if (types.Count > 1)
        {
            result["anyOf"] = new JsonArray(types.Select(type => (JsonNode)new JsonObject { ["type"] = type }).ToArray());
        }

        AddString(result, "format", schema.Format);
        AddNumbers(result, schema);
        AddNumber(result, "maxLength", schema.MaxLength);
        AddNumber(result, "minLength", schema.MinLength);
        AddString(result, "pattern", schema.Pattern);

        if (schema.Items is { } items)
        {
            result["items"] = ConvertSchema(items);
        }

        AddNumber(result, "maxItems", schema.MaxItems);
        AddNumber(result, "minItems", schema.MinItems);
        if (schema.UniqueItems is { } uniqueItems)
        {
            result["uniqueItems"] = uniqueItems;
        }

        if (schema.Properties is { Count: > 0 } properties)
        {
            var converted = new JsonObject();
            foreach (var (name, property) in properties)
            {
                converted[name] = ConvertSchema(property);
            }

            result["properties"] = converted;
        }

        if (schema.Required is { Count: > 0 } required)
        {
            result["required"] = new JsonArray(required.Select(name => (JsonNode)name).ToArray());
        }

        if (schema.AdditionalProperties is { } additionalProperties)
        {
            result["additionalProperties"] = additionalProperties is FalseApiSchema
                ? false
                : ConvertSchema(additionalProperties);
        }

        AddNumber(result, "maxProperties", schema.MaxProperties);
        AddNumber(result, "minProperties", schema.MinProperties);

        AddSchemas(result, "allOf", schema.AllOf);
        var oneOfIsNullable = AddAlternatives(result, "oneOf", schema.OneOf);
        var anyOfIsNullable = AddAlternatives(result, "anyOf", schema.AnyOf);
        nullable |= oneOfIsNullable || anyOfIsNullable;
        if (schema.Not is { } not)
        {
            result["not"] = ConvertSchema(not);
        }

        if (schema.Enum is { Count: > 0 } values)
        {
            result["enum"] = new JsonArray(values.Select(value => value?.GetNode()?.DeepClone()).ToArray());
        }
        else if (schema.Const?.GetNode() is { } constant)
        {
            result["enum"] = new JsonArray(constant.DeepClone());
        }
        else if (schema.Type == SchemaType.Null)
        {
            // A schema of type null: the only value it allows.
            result["enum"] = new JsonArray((JsonNode?)null);
        }

        if (schema.Default?.GetNode() is { } defaultValue)
        {
            result["default"] = defaultValue.DeepClone();
        }

        if (schema.Examples is { Count: > 0 } examples && examples[0]?.GetNode() is { } example)
        {
            result["example"] = example.DeepClone();
        }

        if (!string.IsNullOrEmpty(schema.Discriminator))
        {
            result["discriminator"] = new JsonObject { ["propertyName"] = schema.Discriminator };
        }

        if (nullable)
        {
            result["nullable"] = true;
        }

        AddFlag(result, "readOnly", schema.ReadOnly);
        AddFlag(result, "writeOnly", schema.WriteOnly);
        AddFlag(result, "deprecated", schema.Deprecated);

        if (schema.ExternalDocs is { Url: { } url } externalDocs)
        {
            var docs = new JsonObject { ["url"] = url.ToString() };
            AddString(docs, "description", externalDocs.Description);
            result["externalDocs"] = docs;
        }

        foreach (var (name, extension) in schema.Extensions ?? new Dictionary<string, ByteBard.AsyncAPI.Models.Interfaces.IAsyncApiExtension>())
        {
            if (name.StartsWith("x-", StringComparison.Ordinal) && extension is not null)
            {
                result[name] = ExtensionValue(extension);
            }
        }

        return result;
    }

    private JsonObject ConvertReference(string reference)
    {
        if (!reference.StartsWith(SchemaReferencePrefix, StringComparison.Ordinal))
        {
            return new JsonObject { ["$ref"] = reference };
        }

        var key = reference[SchemaReferencePrefix.Length..];
        if (_inlining.Contains(key)
            || document.Components?.Schemas?.TryGetValue(key, out var target) != true
            || target?.Schema is not AsyncApiJsonSchema targetSchema)
        {
            // A recursive reference, or one this document cannot resolve: it points at the JSON Schema component,
            // which stays in the document.
            return new JsonObject { ["$ref"] = reference };
        }

        _inlining.Add(key);
        try
        {
            return ConvertSchema(targetSchema);
        }
        finally
        {
            _inlining.Remove(key);
        }
    }

    /// <summary>
    /// Writes <c>oneOf</c> or <c>anyOf</c> without its <c>null</c> branches, which OpenAPI 3.0 expresses as
    /// <c>nullable</c>. Returns whether there was one.
    /// </summary>
    private bool AddAlternatives(JsonObject result, string keyword, IList<AsyncApiJsonSchema>? alternatives)
    {
        if (alternatives is not { Count: > 0 })
        {
            return false;
        }

        var nonNull = alternatives.Where(alternative => !IsNullOnly(alternative)).ToList();
        if (nonNull.Count > 0)
        {
            if (result[keyword] is JsonArray existing)
            {
                // The anyOf of several types is there already: both must hold.
                result.Remove(keyword);
                result["allOf"] = MergeAllOf(result["allOf"] as JsonArray,
                    new JsonObject { [keyword] = existing },
                    new JsonObject { [keyword] = new JsonArray(nonNull.Select(alternative => (JsonNode)ConvertSchema(alternative)).ToArray()) });
            }
            else
            {
                result[keyword] = new JsonArray(nonNull.Select(alternative => (JsonNode)ConvertSchema(alternative)).ToArray());
            }
        }

        return nonNull.Count < alternatives.Count;
    }

    private static JsonArray MergeAllOf(JsonArray? allOf, params JsonObject[] schemas)
    {
        var merged = allOf is null ? new JsonArray() : (JsonArray)allOf.DeepClone();
        foreach (var schema in schemas)
        {
            merged.Add((JsonNode)schema);
        }

        return merged;
    }

    private void AddSchemas(JsonObject result, string keyword, IList<AsyncApiJsonSchema>? schemas)
    {
        if (schemas is { Count: > 0 })
        {
            result[keyword] = new JsonArray(schemas.Select(schema => (JsonNode)ConvertSchema(schema)).ToArray());
        }
    }

    private static bool IsNullOnly(AsyncApiJsonSchema schema) =>
        schema is not AsyncApiJsonSchemaReference && schema.Type == SchemaType.Null;

    /// <summary>
    /// Writes the numeric bounds. A numeric <c>exclusiveMinimum</c>/<c>exclusiveMaximum</c> becomes the bound with a
    /// <see langword="true"/> flag, unless the inclusive bound given beside it is the tighter one.
    /// </summary>
    private static void AddNumbers(JsonObject result, AsyncApiJsonSchema schema)
    {
        AddNumber(result, "multipleOf", schema.MultipleOf);
        AddBound(result, "maximum", "exclusiveMaximum", schema.Maximum, schema.ExclusiveMaximum,
            (exclusive, inclusive) => exclusive <= inclusive);
        AddBound(result, "minimum", "exclusiveMinimum", schema.Minimum, schema.ExclusiveMinimum,
            (exclusive, inclusive) => exclusive >= inclusive);
    }

    private static void AddBound(JsonObject result, string keyword, string exclusiveKeyword, double? inclusive,
        double? exclusive, Func<double, double, bool> exclusiveIsTighter)
    {
        if (exclusive is { } exclusiveValue && (inclusive is not { } inclusiveValue || exclusiveIsTighter(exclusiveValue, inclusiveValue)))
        {
            result[keyword] = exclusiveValue;
            result[exclusiveKeyword] = true;
        }
        else
        {
            AddNumber(result, keyword, inclusive);
        }
    }

    private static List<string> SchemaTypes(SchemaType? type)
    {
        var types = new List<string>();
        if (type is not { } flags)
        {
            return types;
        }

        foreach (var (flag, name) in TypeNames)
        {
            if (flags.HasFlag(flag))
            {
                types.Add(name);
            }
        }

        return types;
    }

    private static readonly (SchemaType Flag, string Name)[] TypeNames =
    [
        (SchemaType.Object, "object"),
        (SchemaType.Array, "array"),
        (SchemaType.String, "string"),
        (SchemaType.Integer, "integer"),
        (SchemaType.Number, "number"),
        (SchemaType.Boolean, "boolean"),
    ];

    private static JsonNode? ExtensionValue(ByteBard.AsyncAPI.Models.Interfaces.IAsyncApiExtension extension)
    {
        if (extension is AsyncApiAny any)
        {
            return any.GetNode()?.DeepClone();
        }

        using var text = new StringWriter();
        extension.Write(new AsyncApiJsonWriter(text));
        return JsonNode.Parse(text.ToString());
    }

    private static void AddString(JsonObject result, string keyword, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            result[keyword] = value;
        }
    }

    private static void AddNumber(JsonObject result, string keyword, double? value)
    {
        if (value is { } number)
        {
            result[keyword] = number;
        }
    }

    private static void AddNumber(JsonObject result, string keyword, int? value)
    {
        if (value is { } number)
        {
            result[keyword] = number;
        }
    }

    private static void AddFlag(JsonObject result, string keyword, bool value)
    {
        if (value)
        {
            result[keyword] = true;
        }
    }
}
