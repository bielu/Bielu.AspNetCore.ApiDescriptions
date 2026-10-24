using System.Text.Json.Nodes;
using ByteBard.AsyncAPI.Models;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Avro.Tests.Unit;

public class JsonSchemaToAvroConverterTests
{
    [Fact]
    public void Convert_ObjectWithOptionalProperty_MakesANullUnionDefaultingToNull()
    {
        // Arrange
        var schema = Object(new() { ["sku"] = String(), ["note"] = String() }, required: ["sku"]);
        schema.Description = "An order line.";

        // Act
        var record = Convert(schema, "orderLine").ShouldBeOfType<AvroRecord>();

        // Assert
        record.Name.ShouldBe("OrderLine");
        record.Doc.ShouldBe("An order line.");
        record.Fields[0].Type.ShouldBeOfType<AvroPrimitive>().Type.ShouldBe("string");
        record.Fields[0].Default.ShouldBeNull();
        var note = record.Fields[1].Type.ShouldBeOfType<AvroUnion>();
        note.Types.Select(type => type.Type).ShouldBe(["null", "string"]);
        record.Fields[1].Default.ShouldNotBeNull();
    }

    [Fact]
    public void Convert_NullableType_PutsNullFirst()
    {
        // Arrange
        var schema = new AsyncApiJsonSchema { Type = SchemaType.Integer | SchemaType.Null, Format = "int32" };

        // Act
        var union = Convert(schema, "count").ShouldBeOfType<AvroUnion>();

        // Assert
        union.Types.Select(type => type.Type).ShouldBe(["null", "int"]);
    }

    [Theory]
    [InlineData(SchemaType.Integer, "int64", "long")]
    [InlineData(SchemaType.Integer, null, "long")]
    [InlineData(SchemaType.Integer, "int32", "int")]
    [InlineData(SchemaType.Number, "float", "float")]
    [InlineData(SchemaType.Number, "double", "double")]
    [InlineData(SchemaType.Boolean, null, "boolean")]
    [InlineData(SchemaType.String, "byte", "bytes")]
    [InlineData(SchemaType.String, "email", "string")]
    public void Convert_Primitive_MapsToTheAvroPrimitive(SchemaType type, string? format, string expected)
    {
        // Act
        var converted = Convert(new AsyncApiJsonSchema { Type = type, Format = format }, "value");

        // Assert
        converted.ShouldBeOfType<AvroPrimitive>().Type.ShouldBe(expected);
    }

    [Theory]
    [InlineData("uuid", "string", "uuid")]
    [InlineData("date-time", "long", "timestamp-millis")]
    [InlineData("date", "int", "date")]
    [InlineData("time", "int", "time-millis")]
    public void Convert_StringFormat_MapsToTheLogicalType(string format, string type, string logicalType)
    {
        // Act
        var converted = Convert(new AsyncApiJsonSchema { Type = SchemaType.String, Format = format }, "value");

        // Assert
        var logical = converted.ShouldBeOfType<AvroLogicalTypeSchema>();
        logical.Type.ShouldBe(type);
        logical.LogicalType.ShouldBe(logicalType);
    }

    [Fact]
    public void Convert_StringEnum_MakesAnAvroEnum()
    {
        // Arrange
        var schema = new AsyncApiJsonSchema
        {
            Type = SchemaType.String,
            Enum = [new AsyncApiAny(JsonValue.Create("Placed")), new AsyncApiAny(JsonValue.Create("Shipped"))],
        };

        // Act
        var avroEnum = Convert(schema, "status", "com.example").ShouldBeOfType<AvroEnum>();

        // Assert
        avroEnum.Name.ShouldBe("Status");
        avroEnum.Namespace.ShouldBe("com.example");
        avroEnum.Symbols.ShouldBe(["Placed", "Shipped"]);
    }

    [Fact]
    public void Convert_EnumWithSymbolsThatAreNotNames_StaysItsType()
    {
        // Arrange
        var schema = new AsyncApiJsonSchema
        {
            Type = SchemaType.String,
            Enum = [new AsyncApiAny(JsonValue.Create("in-stock")), new AsyncApiAny(JsonValue.Create("sold out"))],
        };

        // Act & Assert
        Convert(schema, "availability").ShouldBeOfType<AvroPrimitive>().Type.ShouldBe("string");
    }

    [Fact]
    public void Convert_ArrayAndDictionary_MakeArrayAndMap()
    {
        // Arrange
        var schema = Object(new()
        {
            ["tags"] = new AsyncApiJsonSchema { Type = SchemaType.Array, Items = String() },
            ["attributes"] = new AsyncApiJsonSchema { Type = SchemaType.Object, AdditionalProperties = String() },
            ["extra"] = new AsyncApiJsonSchema { Type = SchemaType.Object },
        }, required: ["tags", "attributes", "extra"]);

        // Act
        var record = Convert(schema, "order").ShouldBeOfType<AvroRecord>();

        // Assert
        record.Fields[0].Type.ShouldBeOfType<AvroArray>().Items.Type.ShouldBe("string");
        record.Fields[1].Type.ShouldBeOfType<AvroMap>().Values.Type.ShouldBe("string");
        record.Fields[2].Type.ShouldBeOfType<AvroMap>().Values.Type.ShouldBe("string");
    }

    [Fact]
    public void Convert_OneOf_MakesAUnion()
    {
        // Arrange
        var schema = new AsyncApiJsonSchema { OneOf = [String(), new AsyncApiJsonSchema { Type = SchemaType.Boolean }] };

        // Act
        var union = Convert(schema, "value").ShouldBeOfType<AvroUnion>();

        // Assert
        union.Types.Select(type => type.Type).ShouldBe(["string", "boolean"]);
    }

    [Fact]
    public void Convert_SeveralTypes_MakesAUnionWithoutDuplicates()
    {
        // Arrange
        var schema = new AsyncApiJsonSchema
        {
            AnyOf = [String(), String(), new AsyncApiJsonSchema { Type = SchemaType.String | SchemaType.Integer }],
        };

        // Act
        var union = Convert(schema, "value").ShouldBeOfType<AvroUnion>();

        // Assert
        union.Types.Select(type => type.Type).ShouldBe(["string", "long"]);
    }

    [Fact]
    public void Convert_SingleAllOf_UnwrapsIt()
    {
        // Act & Assert
        Convert(new AsyncApiJsonSchema { AllOf = [String()] }, "value").ShouldBeOfType<AvroPrimitive>().Type.ShouldBe("string");
    }

    [Fact]
    public void Convert_SelfReference_RefersToTheRecordByName()
    {
        // Arrange
        var document = new AsyncApiDocument
        {
            Components = new AsyncApiComponents
            {
                Schemas = new Dictionary<string, AsyncApiMultiFormatSchema>
                {
                    ["category"] = new()
                    {
                        Schema = Object(new()
                        {
                            ["name"] = String(),
                            ["parent"] = new AsyncApiJsonSchemaReference("#/components/schemas/category"),
                        }, required: ["name", "parent"]),
                    },
                },
            },
        };

        // Act
        var record = new JsonSchemaToAvroConverter(document, "com.example")
            .Convert(new AsyncApiJsonSchemaReference("#/components/schemas/category"), "ignored")
            .ShouldBeOfType<AvroRecord>();

        // Assert
        record.Name.ShouldBe("Category");
        record.Fields[1].Type.ShouldBeOfType<AvroNamedType>().Name.ShouldBe("com.example.Category");
    }

    [Fact]
    public void Convert_UnknownReference_FallsBackToString()
    {
        // Act & Assert
        new JsonSchemaToAvroConverter(new AsyncApiDocument(), null)
            .Convert(new AsyncApiJsonSchemaReference("#/components/schemas/missing"), "value")
            .ShouldBeOfType<AvroPrimitive>().Type.ShouldBe("string");
    }

    [Theory]
    [InlineData("orders.order-placed", "orders_order_placed")]
    [InlineData("1st", "_1st")]
    [InlineData("", "_")]
    [InlineData("valid_Name1", "valid_Name1")]
    public void AvroName_ReplacesCharactersAvroDoesNotAllow(string value, string expected)
    {
        // Act & Assert
        JsonSchemaToAvroConverter.AvroName(value).ShouldBe(expected);
    }

    private static AsyncApiAvroSchema Convert(AsyncApiJsonSchema schema, string name, string? avroNamespace = null) =>
        new JsonSchemaToAvroConverter(new AsyncApiDocument(), avroNamespace).Convert(schema, name);

    private static AsyncApiJsonSchema String() => new() { Type = SchemaType.String };

    private static AsyncApiJsonSchema Object(Dictionary<string, AsyncApiJsonSchema> properties, string[] required) => new()
    {
        Type = SchemaType.Object,
        Properties = properties,
        Required = new HashSet<string>(required),
    };
}
