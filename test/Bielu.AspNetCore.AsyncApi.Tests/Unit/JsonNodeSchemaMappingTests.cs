using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
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
/// Unit tests for the <see cref="JsonNodeSchemaExtensions"/> mappers that translate validation attributes,
/// default values and polymorphism options into JSON Schema keywords.
/// </summary>
public class JsonNodeSchemaMappingTests
{
    private static JsonObject StringSchema() => new() { [AsyncApiJsonSchemaKeywords.TypeKeyword] = "string" };

    private static JsonObject ArraySchema() => new() { [AsyncApiJsonSchemaKeywords.TypeKeyword] = "array" };

    [Fact]
    public void ApplyValidationAttributes_IntegerRange_SetsInclusiveBounds()
    {
        // Arrange
        var schema = new JsonObject();

        // Act
        schema.ApplyValidationAttributes([new RangeAttribute(1, 10)]);

        // Assert
        schema[AsyncApiJsonSchemaKeywords.MinimumKeyword]!.GetValue<decimal>().ShouldBe(1m);
        schema[AsyncApiJsonSchemaKeywords.MaximumKeyword]!.GetValue<decimal>().ShouldBe(10m);
    }

    [Fact]
    public void ApplyValidationAttributes_ExclusiveRange_SetsExclusiveBounds()
    {
        // Arrange
        var schema = new JsonObject();
        var range = new RangeAttribute(1, 10) { MinimumIsExclusive = true, MaximumIsExclusive = true };

        // Act
        schema.ApplyValidationAttributes([range]);

        // Assert
        schema[AsyncApiJsonSchemaKeywords.ExclusiveMinimum]!.GetValue<decimal>().ShouldBe(1m);
        schema[AsyncApiJsonSchemaKeywords.ExclusiveMaximum]!.GetValue<decimal>().ShouldBe(10m);
        schema.ContainsKey(AsyncApiJsonSchemaKeywords.MinimumKeyword).ShouldBeFalse();
        schema.ContainsKey(AsyncApiJsonSchemaKeywords.MaximumKeyword).ShouldBeFalse();
    }

    [Fact]
    public void ApplyValidationAttributes_DoubleRange_ParsesInInvariantCulture()
    {
        // Arrange
        var schema = new JsonObject();
        var previousCulture = CultureInfo.CurrentCulture;
        // A comma decimal separator would mangle "0.5" if the current culture were used.
        CultureInfo.CurrentCulture = new CultureInfo("pl-PL");

        try
        {
            // Act
            schema.ApplyValidationAttributes([new RangeAttribute(0.5, 99.5)]);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }

        // Assert
        schema[AsyncApiJsonSchemaKeywords.MinimumKeyword]!.GetValue<decimal>().ShouldBe(0.5m);
        schema[AsyncApiJsonSchemaKeywords.MaximumKeyword]!.GetValue<decimal>().ShouldBe(99.5m);
    }

    [Fact]
    public void ApplyValidationAttributes_StringRangeWithInvariantCulture_ParsesLimits()
    {
        // Arrange
        var schema = new JsonObject();
        var range = new RangeAttribute(typeof(decimal), "1.25", "2.75") { ParseLimitsInInvariantCulture = true };

        // Act
        schema.ApplyValidationAttributes([range]);

        // Assert
        schema[AsyncApiJsonSchemaKeywords.MinimumKeyword]!.GetValue<decimal>().ShouldBe(1.25m);
        schema[AsyncApiJsonSchemaKeywords.MaximumKeyword]!.GetValue<decimal>().ShouldBe(2.75m);
    }

    [Fact]
    public void ApplyValidationAttributes_UnparsableRange_LeavesBoundsUnset()
    {
        // Arrange
        var schema = new JsonObject();
        var range = new RangeAttribute(typeof(DateTime), "2024-01-01", "2024-12-31");

        // Act
        schema.ApplyValidationAttributes([range]);

        // Assert
        schema.ContainsKey(AsyncApiJsonSchemaKeywords.MinimumKeyword).ShouldBeFalse();
        schema.ContainsKey(AsyncApiJsonSchemaKeywords.MaximumKeyword).ShouldBeFalse();
    }

    [Fact]
    public void ApplyValidationAttributes_StringAttributes_SetFormatPatternAndLengths()
    {
        // Arrange
        var pattern = StringSchema();
        var url = StringSchema();
        var base64 = StringSchema();
        var stringLength = StringSchema();

        // Act
        pattern.ApplyValidationAttributes([new RegularExpressionAttribute("^[A-Z]+$")]);
        url.ApplyValidationAttributes([new UrlAttribute()]);
        base64.ApplyValidationAttributes([new Base64StringAttribute()]);
        stringLength.ApplyValidationAttributes([new StringLengthAttribute(10) { MinimumLength = 2 }]);

        // Assert
        pattern[AsyncApiJsonSchemaKeywords.PatternKeyword]!.GetValue<string>().ShouldBe("^[A-Z]+$");
        url[AsyncApiJsonSchemaKeywords.FormatKeyword]!.GetValue<string>().ShouldBe("uri");
        base64[AsyncApiJsonSchemaKeywords.FormatKeyword]!.GetValue<string>().ShouldBe("byte");
        stringLength[AsyncApiJsonSchemaKeywords.MinLengthKeyword]!.GetValue<int>().ShouldBe(2);
        stringLength[AsyncApiJsonSchemaKeywords.MaxLengthKeyword]!.GetValue<int>().ShouldBe(10);
    }

    [Fact]
    public void ApplyValidationAttributes_LengthAttributesOnString_SetCharacterLengths()
    {
        // Arrange
        var minMax = StringSchema();
        var length = StringSchema();

        // Act
        minMax.ApplyValidationAttributes([new MinLengthAttribute(2), new MaxLengthAttribute(5)]);
        length.ApplyValidationAttributes([new LengthAttribute(3, 7)]);

        // Assert
        minMax[AsyncApiJsonSchemaKeywords.MinLengthKeyword]!.GetValue<int>().ShouldBe(2);
        minMax[AsyncApiJsonSchemaKeywords.MaxLengthKeyword]!.GetValue<int>().ShouldBe(5);
        length[AsyncApiJsonSchemaKeywords.MinLengthKeyword]!.GetValue<int>().ShouldBe(3);
        length[AsyncApiJsonSchemaKeywords.MaxLengthKeyword]!.GetValue<int>().ShouldBe(7);
    }

    [Fact]
    public void ApplyValidationAttributes_LengthAttributesOnArray_SetItemCounts()
    {
        // Arrange
        var minMax = ArraySchema();
        var length = new JsonObject { [AsyncApiJsonSchemaKeywords.TypeKeyword] = new JsonArray("array", "null") };

        // Act
        minMax.ApplyValidationAttributes([new MinLengthAttribute(1), new MaxLengthAttribute(3)]);
        length.ApplyValidationAttributes([new LengthAttribute(2, 4)]);

        // Assert
        minMax[AsyncApiJsonSchemaKeywords.MinItemsKeyword]!.GetValue<int>().ShouldBe(1);
        minMax[AsyncApiJsonSchemaKeywords.MaxItemsKeyword]!.GetValue<int>().ShouldBe(3);
        minMax.ContainsKey(AsyncApiJsonSchemaKeywords.MaxLengthKeyword).ShouldBeFalse();
        length[AsyncApiJsonSchemaKeywords.MinItemsKeyword]!.GetValue<int>().ShouldBe(2);
        length[AsyncApiJsonSchemaKeywords.MaxItemsKeyword]!.GetValue<int>().ShouldBe(4);
    }

    [Fact]
    public void ApplyValidationAttributes_UnknownSchemaType_Throws()
    {
        // Arrange
        var schema = new JsonObject { [AsyncApiJsonSchemaKeywords.TypeKeyword] = new JsonArray("not-a-type") };

        // Act & Assert
        Should.Throw<InvalidOperationException>(() => schema.ApplyValidationAttributes([new MaxLengthAttribute(3)]));
    }

    [Fact]
    public void ApplyValidationAttributes_LastAttributeWins()
    {
        // Arrange
        var schema = StringSchema();

        // Act
        schema.ApplyValidationAttributes([new Base64StringAttribute(), new UrlAttribute()]);

        // Assert
        schema[AsyncApiJsonSchemaKeywords.FormatKeyword]!.GetValue<string>().ShouldBe("uri");
    }

    [Fact]
    public void ApplyDefaultValue_NullTypeInfo_DoesNothing()
    {
        // Arrange
        var schema = new JsonObject();

        // Act
        schema.ApplyDefaultValue(5, jsonTypeInfo: null);

        // Assert
        schema.Count.ShouldBe(0);
    }

    [Fact]
    public void ApplyDefaultValue_InlineSchema_SetsDefaultKeyword()
    {
        // Arrange
        var schema = new JsonObject();
        var typeInfo = JsonSerializerOptions.Default.GetTypeInfo(typeof(int));

        // Act
        schema.ApplyDefaultValue(42, typeInfo);

        // Assert
        schema[AsyncApiJsonSchemaKeywords.DefaultKeyword]!.GetValue<int>().ShouldBe(42);
    }

    [Fact]
    public void ApplyDefaultValue_NullValue_SetsNullDefault()
    {
        // Arrange
        var schema = new JsonObject();
        var typeInfo = JsonSerializerOptions.Default.GetTypeInfo(typeof(string));

        // Act
        schema.ApplyDefaultValue(null, typeInfo);

        // Assert
        schema.ContainsKey(AsyncApiJsonSchemaKeywords.DefaultKeyword).ShouldBeTrue();
        schema[AsyncApiJsonSchemaKeywords.DefaultKeyword].ShouldBeNull();
    }

    [Fact]
    public void ApplyDefaultValue_ComponentizedSchema_WritesToReferenceSlot()
    {
        // Arrange
        var schema = new JsonObject { [AsyncApiConstants.Id] = "Widget" };
        var typeInfo = JsonSerializerOptions.Default.GetTypeInfo(typeof(int));

        // Act
        schema.ApplyDefaultValue(7, typeInfo);

        // Assert
        schema.ContainsKey(AsyncApiJsonSchemaKeywords.DefaultKeyword).ShouldBeFalse();
        schema[AsyncApiConstants.DollarRef]!.GetValue<int>().ShouldBe(7);
    }

    [Fact]
    public void WillBeComponentized_ReflectsSchemaId()
    {
        // Arrange
        var withId = new JsonObject { [AsyncApiConstants.Id] = "Widget" };
        var emptyId = new JsonObject { [AsyncApiConstants.Id] = "" };
        var numericId = new JsonObject { [AsyncApiConstants.Id] = 3 };

        // Act & Assert
        withId.WillBeComponentized(out var schemaId).ShouldBeTrue();
        schemaId.ShouldBe("Widget");
        emptyId.WillBeComponentized().ShouldBeFalse();
        numericId.WillBeComponentized().ShouldBeFalse();
        new JsonObject().WillBeComponentized().ShouldBeFalse();
    }

    private sealed class QueryArgs
    {
        [DefaultValue(25)]
        public int PageSize { get; set; }
    }

    [JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
    [JsonDerivedType(typeof(Cat), "cat")]
    [JsonDerivedType(typeof(Dog), "dog")]
    [JsonDerivedType(typeof(Fish))]
    private abstract class Animal
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class Cat : Animal
    {
        public bool Indoor { get; set; }
    }

    private sealed class Dog : Animal
    {
        public int Barks { get; set; }
    }

    private sealed class Fish : Animal
    {
    }

    [JsonDerivedType(typeof(Circle), "circle")]
    private class Shape
    {
        public int Sides { get; set; }
    }

    private sealed class Circle : Shape
    {
        public double Radius { get; set; }
    }

    private static JsonNode ExportSchema(Type type)
    {
        var options = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
        var exporterOptions = new JsonSchemaExporterOptions
        {
            TransformSchemaNode = (context, schema) =>
            {
                if (schema.GetValueKind() == JsonValueKind.True)
                {
                    schema = new JsonObject();
                }
                schema.ApplySchemaReferenceId(context, typeInfo => typeInfo.Type.Name);
                schema.MapPolymorphismOptionsToDiscriminator(context, typeInfo => typeInfo.Type.Name);
                return schema;
            }
        };
        return JsonSchemaExporter.GetJsonSchemaAsNode(options, type, exporterOptions);
    }

    [Fact]
    public void MapPolymorphismOptionsToDiscriminator_AbstractBase_MapsDiscriminatorValues()
    {
        // Act
        var schema = ExportSchema(typeof(Animal));

        // Assert
        schema[AsyncApiJsonSchemaKeywords.DiscriminatorKeyword]!.GetValue<string>().ShouldBe("kind");
        var mapping = schema[AsyncApiJsonSchemaKeywords.DiscriminatorMappingKeyword].ShouldBeOfType<JsonObject>();
        mapping["cat"]!.GetValue<string>().ShouldBe("AnimalCat");
        mapping["dog"]!.GetValue<string>().ShouldBe("AnimalDog");
        // Derived types without a discriminator value have no mapping entry.
        mapping.Count.ShouldBe(2);
        schema[AsyncApiConstants.Id]!.GetValue<string>().ShouldBe(nameof(Animal));
    }

    [Fact]
    public void MapPolymorphismOptionsToDiscriminator_NonAbstractBaseNotSelfDerived_SkipsDiscriminator()
    {
        // Act
        var schema = ExportSchema(typeof(Shape));

        // Assert
        schema[AsyncApiJsonSchemaKeywords.DiscriminatorKeyword].ShouldBeNull();
        schema[AsyncApiJsonSchemaKeywords.DiscriminatorMappingKeyword].ShouldBeNull();
    }

    [Fact]
    public void ApplySchemaReferenceId_NullReferenceId_LeavesIdUnset()
    {
        // Arrange
        var options = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
        var exporterOptions = new JsonSchemaExporterOptions
        {
            TransformSchemaNode = (context, schema) =>
            {
                schema.ApplySchemaReferenceId(context, _ => null);
                return schema;
            }
        };

        // Act
        var schema = JsonSchemaExporter.GetJsonSchemaAsNode(options, typeof(QueryArgs), exporterOptions);

        // Assert
        schema[AsyncApiConstants.Id].ShouldBeNull();
    }
}
