using System.Text;
using System.Text.Json.Nodes;
using ByteBard.AsyncAPI.Models;

namespace Bielu.AspNetCore.AsyncApi.Avro;

/// <summary>
/// Converts the JSON Schema of a message payload into an Avro schema. One converter handles one payload: Avro
/// defines each named type once per schema and refers to it by name after that.
/// </summary>
/// <remarks>
/// JSON Schema can say less than Avro about numbers: an <c>integer</c> is an Avro <c>int</c> only with the <c>int32</c> format,
/// and otherwise a <c>long</c>, which every integer fits in; a <c>number</c> is a <c>double</c> unless its format is <c>float</c>.
/// </remarks>
internal sealed class JsonSchemaToAvroConverter(AsyncApiDocument document, string? avroNamespace)
{
    private const string SchemaReferencePrefix = "#/components/schemas/";
    // Each named type with the schema that defined it, so that another schema whose hint gives the same name gets a
    // name of its own rather than a reference to a type of a different shape.
    private readonly Dictionary<string, AsyncApiJsonSchema> _namedTypes = new(StringComparer.Ordinal);

    /// <summary>Converts a payload schema, naming its top-level record after <paramref name="name"/>.</summary>
    public AsyncApiAvroSchema Convert(AsyncApiJsonSchema schema, string name) => ConvertSchema(schema, name);

    private AsyncApiAvroSchema ConvertSchema(AsyncApiJsonSchema schema, string nameHint)
    {
        if (schema is AsyncApiJsonSchemaReference { Reference.Reference: { } reference }
            && reference.StartsWith(SchemaReferencePrefix, StringComparison.Ordinal))
        {
            // Every reference to a component resolves to the same schema, so the type it defined the first time is
            // found again by that schema, not by name.
            var key = reference[SchemaReferencePrefix.Length..];
            return document.Components?.Schemas?.TryGetValue(key, out var target) == true
                   && target.Schema is AsyncApiJsonSchema targetSchema
                ? ConvertSchema(targetSchema, key)
                : String();
        }

        if (schema.Type == SchemaType.Null)
        {
            return new AvroPrimitive(AvroPrimitiveType.Null);
        }

        var nonNullTypes = (schema.Type ?? 0) & ~SchemaType.Null;
        var nullable = schema.Nullable || (schema.Type?.HasFlag(SchemaType.Null) == true && nonNullTypes != 0);

        var converted = ConvertNonNull(schema, nonNullTypes, nameHint);
        return nullable ? Union([new AvroPrimitive(AvroPrimitiveType.Null), converted]) : converted;
    }

    private AsyncApiAvroSchema ConvertNonNull(AsyncApiJsonSchema schema, SchemaType types, string nameHint)
    {
        if (schema.AllOf is [var single] && schema.Properties is not { Count: > 0 })
        {
            return ConvertSchema(single, nameHint);
        }

        if (schema.OneOf is { Count: > 0 } || schema.AnyOf is { Count: > 0 })
        {
            var alternatives = (schema.OneOf is { Count: > 0 } ? schema.OneOf : schema.AnyOf!)
                .Select((alternative, index) => ConvertSchema(alternative, $"{nameHint}{index + 1}"))
                .ToList();
            return Union(alternatives);
        }

        if (schema.Enum is { Count: > 0 } && TryConvertEnum(schema, nameHint) is { } avroEnum)
        {
            return avroEnum;
        }

        if (schema.Properties is { Count: > 0 } || types == SchemaType.Object)
        {
            return ConvertObject(schema, nameHint);
        }

        var alternativesByType = Enum.GetValues<SchemaType>()
            .Where(type => type != SchemaType.Null && types.HasFlag(type))
            .Select(type => ConvertType(schema, type, nameHint))
            .ToList();
        return alternativesByType switch
        {
            [] => String(),
            [var only] => only,
            _ => Union(alternativesByType),
        };
    }

    private AsyncApiAvroSchema ConvertType(AsyncApiJsonSchema schema, SchemaType type, string nameHint) => type switch
    {
        SchemaType.Boolean => new AvroPrimitive(AvroPrimitiveType.Boolean),
        SchemaType.Integer => new AvroPrimitive(schema.Format == "int32" ? AvroPrimitiveType.Int : AvroPrimitiveType.Long),
        SchemaType.Number => new AvroPrimitive(schema.Format == "float" ? AvroPrimitiveType.Float : AvroPrimitiveType.Double),
        SchemaType.Array => new AvroArray
        {
            Items = schema.Items is { } items ? ConvertSchema(items, nameHint + "Item") : String(),
        },
        SchemaType.Object => ConvertObject(schema, nameHint),
        _ => schema.Format switch
        {
            "uuid" => new AvroLogicalTypeSchema("string", "uuid"),
            "date-time" => new AvroLogicalTypeSchema("long", "timestamp-millis"),
            "date" => new AvroLogicalTypeSchema("int", "date"),
            "time" => new AvroLogicalTypeSchema("int", "time-millis"),
            "byte" or "binary" => new AvroPrimitive(AvroPrimitiveType.Bytes),
            _ => String(),
        },
    };

    private AsyncApiAvroSchema ConvertObject(AsyncApiJsonSchema schema, string nameHint)
    {
        if (schema.Properties is not { Count: > 0 })
        {
            // A dictionary, or a free-form object, which Avro can only carry as a map.
            return new AvroMap
            {
                Values = schema.AdditionalProperties is { } values and not FalseApiSchema
                    ? ConvertSchema(values, nameHint + "Value")
                    : String(),
            };
        }

        if (!TryDefineName(schema, nameHint, out var name))
        {
            return new AvroNamedType(QualifiedName(name), null!);
        }

        var record = new AvroRecord
        {
            Name = name,
            Namespace = avroNamespace,
            Doc = schema.Description,
            Fields = [],
        };

        foreach (var (propertyName, property) in schema.Properties)
        {
            var fieldType = ConvertSchema(property, nameHint + Pascal(propertyName));
            var optional = schema.Required?.Contains(propertyName) != true;
            if (optional && !IsNullable(fieldType))
            {
                fieldType = Union([new AvroPrimitive(AvroPrimitiveType.Null), fieldType]);
            }

            record.Fields.Add(new AvroField
            {
                Name = AvroName(propertyName),
                Type = fieldType,
                Doc = property.Description,
                // A union's default has to match its first branch, which for an optional field is null.
                // ByteBard writes the string "null" as a JSON null default.
                Default = optional && fieldType is AvroUnion ? new AsyncApiAny("null") : null!,
            });
        }

        return record;
    }

    private AsyncApiAvroSchema? TryConvertEnum(AsyncApiJsonSchema schema, string nameHint)
    {
        var symbols = schema.Enum
            .Select(value => value?.GetNode() is JsonValue node && node.TryGetValue<string>(out var symbol) ? symbol : null)
            .ToList();
        if (symbols.Any(symbol => symbol is null || !IsAvroName(symbol)))
        {
            // Avro symbols must be names; anything else stays a plain value of its type.
            return null;
        }

        if (!TryDefineName(schema, nameHint, out var name))
        {
            return new AvroNamedType(QualifiedName(name), null!);
        }

        return new AvroEnum
        {
            Name = name,
            Namespace = avroNamespace,
            Doc = schema.Description,
            Symbols = symbols.Distinct(StringComparer.Ordinal).ToList()!,
        };
    }

    /// <summary>
    /// Names the type <paramref name="schema"/> defines after <paramref name="nameHint"/>, numbering the name when another
    /// schema already has it. Returns <see langword="false"/> when <paramref name="schema"/> is already defined, with
    /// <paramref name="name"/> set to the name to refer to it by.
    /// </summary>
    private bool TryDefineName(AsyncApiJsonSchema schema, string nameHint, out string name)
    {
        var baseName = TypeName(nameHint);
        name = baseName;
        for (var suffix = 2; _namedTypes.TryGetValue(name, out var owner); suffix++)
        {
            if (ReferenceEquals(owner, schema))
            {
                return false;
            }

            name = $"{baseName}{suffix}";
        }

        _namedTypes.Add(name, schema);
        return true;
    }

    /// <summary>
    /// Builds a union, flattening nested unions and keeping one branch per primitive type, since Avro allows neither,
    /// with <c>null</c> first so that an optional field can default to it.
    /// </summary>
    private static AsyncApiAvroSchema Union(IEnumerable<AsyncApiAvroSchema> branches)
    {
        var flattened = new List<AsyncApiAvroSchema>();
        foreach (var branch in branches.SelectMany(branch => branch is AvroUnion union ? union.Types : [branch]))
        {
            if (branch is AvroPrimitive primitive && flattened.OfType<AvroPrimitive>().Any(existing => existing.Type == primitive.Type))
            {
                continue;
            }

            flattened.Add(branch);
        }

        // OrderBy is stable, so the other branches keep the order the schema gave them.
        var ordered = flattened.OrderBy(branch => !IsNull(branch)).ToList();
        return ordered is [var only] ? only : new AvroUnion { Types = ordered };
    }

    private static bool IsNull(AsyncApiAvroSchema schema) =>
        schema is AvroPrimitive { Type: "null" };

    private static bool IsNullable(AsyncApiAvroSchema schema) =>
        schema is AvroUnion union && union.Types.Any(IsNull);

    private static AvroPrimitive String() => new(AvroPrimitiveType.String);

    private string QualifiedName(string name) => avroNamespace is null ? name : $"{avroNamespace}.{name}";

    /// <summary>Makes <paramref name="value"/> a valid Avro name: <c>[A-Za-z_][A-Za-z0-9_]*</c>.</summary>
    internal static string AvroName(string value)
    {
        var builder = new StringBuilder(value.Length + 1);
        foreach (var character in value)
        {
            builder.Append(char.IsAsciiLetterOrDigit(character) || character == '_' ? character : '_');
        }

        if (builder.Length == 0 || char.IsAsciiDigit(builder[0]))
        {
            builder.Insert(0, '_');
        }

        return builder.ToString();
    }

    private static bool IsAvroName(string value) => value.Length > 0 && AvroName(value) == value;

    /// <summary>The name of a record or enum: a valid Avro name in PascalCase, as Avro type names usually are.</summary>
    private static string TypeName(string value) => Pascal(AvroName(value));

    private static string Pascal(string value) =>
        value.Length == 0 ? value : char.ToUpperInvariant(value[0]) + value[1..];
}
