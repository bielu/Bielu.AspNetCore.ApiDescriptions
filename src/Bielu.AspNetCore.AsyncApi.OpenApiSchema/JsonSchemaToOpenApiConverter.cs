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

    /// <summary>The suffix of the key of a component schema holding the OpenAPI copy of a recursive one.</summary>
    internal const string ComponentKeySuffix = ".openapi";

    // The component schemas being written, so that a recursive reference stays a $ref instead of looping.
    private HashSet<string> _inlining = new(StringComparer.Ordinal);

    // The OpenAPI component written for each recursive JSON Schema component, by the JSON Schema component's key.
    private readonly Dictionary<string, string> _components = new(StringComparer.Ordinal);

    // The AsyncAPI 3 reference to each OpenAPI component, with the AsyncAPI 2 one to write in its place.
    private readonly Dictionary<string, string> _v2References = new(StringComparer.Ordinal);

    /// <summary>Converts a payload schema.</summary>
    public OpenApiSchemaObject Convert(AsyncApiJsonSchema schema) => new(ConvertSchema(schema), _v2References);

    private JsonObject ConvertSchema(AsyncApiJsonSchema schema)
    {
        if (schema is AsyncApiJsonSchemaReference { Reference.Reference: { } reference })
        {
            return ConvertReference(reference);
        }

        if (schema is FalseApiSchema)
        {
            return Impossible();
        }

        var result = new JsonObject();
        AddString(result, "title", schema.Title);
        AddString(result, "description", schema.Description);

        // OpenAPI 3.0's nullable only adds null to the type beside it, so it is written with a single type only.
        var allowsNull = schema.Nullable || schema.Type?.HasFlag(SchemaType.Null) == true;
        var types = SchemaTypes(schema.Type);
        var items = schema.Items is { } itemSchema ? ConvertSchema(itemSchema) : null;
        if (types is [var single])
        {
            result["type"] = single;
            if (single == "array")
            {
                // OpenAPI 3.0 requires items beside type array.
                result["items"] = items ?? new JsonObject();
            }
        }
        else if (types.Count > 1)
        {
            var alternatives = types.Select(type => (JsonNode)TypeAlternative(type, items)).ToList();
            if (allowsNull)
            {
                alternatives.Add(NullOnly());
            }

            result["anyOf"] = new JsonArray(alternatives.ToArray());
        }
        else if (items is not null)
        {
            result["items"] = items;
        }

        AddString(result, "format", schema.Format);
        AddNumbers(result, schema);
        AddNumber(result, "maxLength", schema.MaxLength);
        AddNumber(result, "minLength", schema.MinLength);
        AddString(result, "pattern", schema.Pattern);

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
        AddAlternatives(result, "oneOf", schema.OneOf);
        AddAlternatives(result, "anyOf", schema.AnyOf);
        if (schema.Not is { } not)
        {
            result["not"] = ConvertSchema(not);
        }

        AddEnum(result, schema, types.Count > 0 && !allowsNull);

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

        if (allowsNull && types.Count == 1)
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
        if (document.Components?.Schemas?.TryGetValue(key, out var target) != true
            || target?.Schema is not AsyncApiJsonSchema targetSchema)
        {
            // One this document cannot resolve: kept as it is.
            return new JsonObject { ["$ref"] = reference };
        }

        if (_inlining.Contains(key))
        {
            // A recursive reference cannot be inlined forever: it points at an OpenAPI copy of the component.
            return new JsonObject { ["$ref"] = ComponentReference(key, targetSchema) };
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
    /// Returns the reference to the OpenAPI copy of a component schema, adding the copy the first time. In AsyncAPI 3
    /// the copy is a Multi Format Schema Object declaring the OpenAPI format, so the reference points at its
    /// <c>schema</c>. AsyncAPI 2 component schemas cannot declare a format, so there the copy is written as the
    /// original JSON Schema and the reference points at the original component.
    /// </summary>
    private string ComponentReference(string key, AsyncApiJsonSchema schema)
    {
        if (_components.TryGetValue(key, out var existing))
        {
            return existing;
        }

        var schemas = document.Components!.Schemas;
        var componentKey = key + ComponentKeySuffix;
        for (var index = 2; schemas.ContainsKey(componentKey); index++)
        {
            componentKey = $"{key}{ComponentKeySuffix}{index}";
        }

        var reference = $"{SchemaReferencePrefix}{componentKey}/schema";
        _components[key] = reference;
        _v2References[reference] = SchemaReferencePrefix + key;

        // Reserved before it is converted, so a reference back to it inside resolves to the same copy.
        var component = new AsyncApiMultiFormatSchema { SchemaFormat = OpenApiSchemaPayloadOptions.SchemaFormat };
        schemas[componentKey] = component;

        var inlining = _inlining;
        _inlining = new HashSet<string>(StringComparer.Ordinal) { key };
        try
        {
            component.Schema = new OpenApiSchemaObject(ConvertSchema(schema), _v2References, schema);
        }
        finally
        {
            _inlining = inlining;
        }

        return reference;
    }

    /// <summary>
    /// Writes <c>oneOf</c> or <c>anyOf</c>. A <c>null</c> branch stays a branch of its own, since <c>nullable</c> on
    /// this schema would not let null past the other branches.
    /// </summary>
    private void AddAlternatives(JsonObject result, string keyword, IList<AsyncApiJsonSchema>? alternatives)
    {
        if (alternatives is not { Count: > 0 })
        {
            return;
        }

        var converted = new JsonArray(alternatives.Select(alternative => (JsonNode)ConvertSchema(alternative)).ToArray());
        if (result[keyword] is JsonArray existing)
        {
            // The anyOf of several types is there already: both must hold.
            result.Remove(keyword);
            result["allOf"] = MergeAllOf(result["allOf"] as JsonArray,
                new JsonObject { [keyword] = existing },
                new JsonObject { [keyword] = converted });
        }
        else
        {
            result[keyword] = converted;
        }
    }

    /// <summary>
    /// Writes <c>enum</c> from <c>enum</c> and <c>const</c>: the values both allow. A <c>null</c> value is left out
    /// when the type excludes it. When no value is left, the schema allows nothing.
    /// </summary>
    private static void AddEnum(JsonObject result, AsyncApiJsonSchema schema, bool typeExcludesNull)
    {
        List<JsonNode?>? values = null;
        if (schema.Enum is { Count: > 0 } enumValues)
        {
            values = enumValues.Select(value => value?.GetNode()).ToList();
        }

        if (schema.Const is { } constValue)
        {
            // A const of null is still a const: only an absent one leaves the enum alone.
            var constant = constValue.GetNode();
            values = values is null ? [constant] : values.Where(value => JsonNode.DeepEquals(value, constant)).ToList();
        }
        else if (values is null && schema.Type == SchemaType.Null)
        {
            // A schema of type null: the only value it allows.
            values = [null];
        }

        if (values is null)
        {
            return;
        }

        if (typeExcludesNull)
        {
            values.RemoveAll(value => value is null);
        }

        if (values.Count == 0)
        {
            result["allOf"] = MergeAllOf(result["allOf"] as JsonArray, Impossible());
            return;
        }

        result["enum"] = new JsonArray(values.Select(value => value?.DeepClone()).ToArray());
    }

    private static JsonObject TypeAlternative(string type, JsonObject? items)
    {
        var alternative = new JsonObject { ["type"] = type };
        if (type == "array")
        {
            alternative["items"] = items?.DeepClone() ?? new JsonObject();
        }

        return alternative;
    }

    // OpenAPI 3.0 has no type null: an enum of only null allows null and nothing else.
    private static JsonObject NullOnly() => new() { ["enum"] = new JsonArray((JsonNode?)null) };

    // OpenAPI 3.0 has no boolean schemas: "not anything" says false.
    private static JsonObject Impossible() => new() { ["not"] = new JsonObject() };

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
