using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Bielu.AspNetCore.AsyncApi.Extensions;
using Bielu.AspNetCore.AsyncApi.Schemas;
using ByteBard.AsyncAPI.Models;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="JsonNodeSchemaExtensions.ApplyPrimitiveTypesAndFormats"/>.
///
/// Regression coverage for a bug where numeric properties (float/double/decimal) that
/// System.Text.Json's own <see cref="JsonSchemaExporter"/> reports as a "string" type -
/// e.g. because <see cref="JsonNumberHandlingAttribute"/> with <see cref="JsonNumberHandling.WriteAsString"/>
/// is applied - kept "type": "string" in the generated schema even though the format was
/// correctly set to "float"/"double". <see cref="JsonNodeSchemaExtensions.ApplyPrimitiveTypesAndFormats"/>
/// must force the "type" keyword back to the numeric type it owns for these CLR types.
/// </summary>
public class JsonNodeSchemaExtensionsTests
{
    private sealed class NumericQuantities
    {
        public float PlainFloat { get; set; }

        public double PlainDouble { get; set; }

        public decimal PlainDecimal { get; set; }

        public float? NullableFloat { get; set; }

        [JsonNumberHandling(JsonNumberHandling.WriteAsString)]
        public float StringEncodedFloat { get; set; }

        [JsonNumberHandling(JsonNumberHandling.WriteAsString)]
        public float? NullableStringEncodedFloat { get; set; }
    }

    /// <summary>
    /// Exports the schema for <see cref="NumericQuantities"/>, running the same
    /// <see cref="JsonNodeSchemaExtensions.ApplyPrimitiveTypesAndFormats"/> +
    /// <see cref="JsonNodeSchemaExtensions.ApplyNullabilityContextInfo"/> pipeline that
    /// <c>AsyncApiJsonSchemaService</c> runs for every property, and returns the schema
    /// node for the requested property.
    /// </summary>
    private static JsonNode GetPropertySchema(string propertyName)
    {
        var options = new JsonSerializerOptions
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        };
        var exporterOptions = new JsonSchemaExporterOptions
        {
            TreatNullObliviousAsNonNullable = true,
            TransformSchemaNode = (context, schema) =>
            {
                schema.ApplyPrimitiveTypesAndFormats(context, _ => null);
                if (context.PropertyInfo is { } propertyInfo)
                {
                    schema.ApplyNullabilityContextInfo(propertyInfo);
                }
                return schema;
            }
        };

        var rootSchema = JsonSchemaExporter.GetJsonSchemaAsNode(options, typeof(NumericQuantities), exporterOptions);
        var propertiesNode = rootSchema[AsyncApiJsonSchemaKeywords.PropertiesKeyword]?.AsObject();
        propertiesNode.ShouldNotBeNull();

        var propertySchema = propertiesNode![propertyName];
        propertySchema.ShouldNotBeNull($"Expected a schema for property '{propertyName}'");
        return propertySchema!;
    }

    /// <summary>
    /// Reads the "type" keyword as the set of schema type names it represents, regardless
    /// of whether it was stored as a single value, a flags combination, or a type array.
    /// </summary>
    private static HashSet<SchemaType> GetSchemaTypes(JsonNode schema)
    {
        var typeNode = schema[AsyncApiJsonSchemaKeywords.TypeKeyword];
        var rawType = typeNode is JsonArray typeArray
            ? string.Join(", ", typeArray.Select(t => t!.GetValue<string>()))
            : typeNode?.GetValue<string>();
        rawType.ShouldNotBeNullOrEmpty();
        Enum.TryParse<SchemaType>(rawType, ignoreCase: true, out var parsed).ShouldBeTrue($"Could not parse schema type '{rawType}'");

        var result = new HashSet<SchemaType>();
        foreach (SchemaType candidate in Enum.GetValues<SchemaType>())
        {
            if (candidate != SchemaType.Null || parsed.HasFlag(SchemaType.Null))
            {
                if (parsed.HasFlag(candidate))
                {
                    result.Add(candidate);
                }
            }
        }
        return result;
    }

    [Fact]
    public void PlainFloat_IsTypeNumberWithFloatFormat()
    {
        var schema = GetPropertySchema(nameof(NumericQuantities.PlainFloat));

        GetSchemaTypes(schema).ShouldBe([SchemaType.Number], ignoreOrder: true);
        schema[AsyncApiJsonSchemaKeywords.FormatKeyword]?.GetValue<string>().ShouldBe("float");
    }

    [Fact]
    public void PlainDouble_IsTypeNumberWithDoubleFormat()
    {
        var schema = GetPropertySchema(nameof(NumericQuantities.PlainDouble));

        GetSchemaTypes(schema).ShouldBe([SchemaType.Number], ignoreOrder: true);
        schema[AsyncApiJsonSchemaKeywords.FormatKeyword]?.GetValue<string>().ShouldBe("double");
    }

    [Fact]
    public void PlainDecimal_IsTypeNumberWithDoubleFormat()
    {
        var schema = GetPropertySchema(nameof(NumericQuantities.PlainDecimal));

        GetSchemaTypes(schema).ShouldBe([SchemaType.Number], ignoreOrder: true);
        schema[AsyncApiJsonSchemaKeywords.FormatKeyword]?.GetValue<string>().ShouldBe("double");
    }

    /// <summary>
    /// This is the reported bug: a non-nullable float whose System.Text.Json schema exporter
    /// reports "string" for the wire type (because of WriteAsString number handling) must still
    /// end up as "type": "number" with "format": "float" in the AsyncApi schema.
    /// </summary>
    [Fact]
    public void StringEncodedFloat_IsForcedBackToTypeNumberWithFloatFormat()
    {
        var schema = GetPropertySchema(nameof(NumericQuantities.StringEncodedFloat));

        GetSchemaTypes(schema).ShouldBe([SchemaType.Number], ignoreOrder: true);
        schema[AsyncApiJsonSchemaKeywords.FormatKeyword]?.GetValue<string>().ShouldBe("float");
        schema[AsyncApiJsonSchemaKeywords.PatternKeyword].ShouldBeNull();
    }

    [Fact]
    public void NullableFloat_IsTypeNumberOrNullWithFloatFormat()
    {
        var schema = GetPropertySchema(nameof(NumericQuantities.NullableFloat));

        GetSchemaTypes(schema).ShouldBe([SchemaType.Number, SchemaType.Null], ignoreOrder: true);
        schema[AsyncApiJsonSchemaKeywords.FormatKeyword]?.GetValue<string>().ShouldBe("float");
    }

    /// <summary>
    /// Guards against a regression where forcing "type" back to Number for the WriteAsString
    /// case could drop the "null" branch for a nullable numeric property. The subsequent
    /// ApplyNullabilityContextInfo call (run by AsyncApiJsonSchemaService for every property)
    /// must restore it.
    /// </summary>
    [Fact]
    public void NullableStringEncodedFloat_IsTypeNumberOrNullWithFloatFormat()
    {
        var schema = GetPropertySchema(nameof(NumericQuantities.NullableStringEncodedFloat));

        GetSchemaTypes(schema).ShouldBe([SchemaType.Number, SchemaType.Null], ignoreOrder: true);
        schema[AsyncApiJsonSchemaKeywords.FormatKeyword]?.GetValue<string>().ShouldBe("float");
    }
}
