using System.Text.Json.Nodes;
using ByteBard.AsyncAPI.Models;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.OpenApiSchema.Tests.Unit;

public class JsonSchemaToOpenApiConverterTests
{
    [Fact]
    public void Convert_SeveralTypes_MakesAnAnyOfOfEach()
    {
        // Arrange
        var schema = new AsyncApiJsonSchema { Type = SchemaType.String | SchemaType.Integer | SchemaType.Null };

        // Act
        var converted = Convert(schema);

        // Assert
        converted.ContainsKey("type").ShouldBeFalse();
        converted["anyOf"]!.AsArray().Select(branch => branch!["type"]!.GetValue<string>()).ShouldBe(["string", "integer"]);
        converted["nullable"]!.GetValue<bool>().ShouldBeTrue();
    }

    [Fact]
    public void Convert_OneOfWithANullBranch_DropsItForNullable()
    {
        // Arrange
        var schema = new AsyncApiJsonSchema
        {
            OneOf = [new AsyncApiJsonSchema { Type = SchemaType.Null }, new AsyncApiJsonSchema { Type = SchemaType.String }],
        };

        // Act
        var converted = Convert(schema);

        // Assert
        converted["oneOf"]!.AsArray().ShouldHaveSingleItem()!["type"]!.GetValue<string>().ShouldBe("string");
        converted["nullable"]!.GetValue<bool>().ShouldBeTrue();
    }

    [Fact]
    public void Convert_SeveralTypesWithAnAnyOf_RequiresBoth()
    {
        // Arrange
        var schema = new AsyncApiJsonSchema
        {
            Type = SchemaType.String | SchemaType.Number,
            AnyOf = [new AsyncApiJsonSchema { MinLength = 1 }],
        };

        // Act
        var converted = Convert(schema);

        // Assert
        converted.ContainsKey("anyOf").ShouldBeFalse();
        var allOf = converted["allOf"]!.AsArray();
        allOf.Count.ShouldBe(2);
        allOf[0]!["anyOf"]!.AsArray().Count.ShouldBe(2);
        allOf[1]!["anyOf"]![0]!["minLength"]!.GetValue<int>().ShouldBe(1);
    }

    [Fact]
    public void Convert_NullType_AllowsOnlyNull()
    {
        // Act
        var converted = Convert(new AsyncApiJsonSchema { Type = SchemaType.Null });

        // Assert
        converted["nullable"]!.GetValue<bool>().ShouldBeTrue();
        converted["enum"]!.AsArray().ShouldHaveSingleItem().ShouldBeNull();
    }

    [Theory]
    [InlineData(10d, null, 10d, true)]
    [InlineData(10d, 20d, 10d, true)]
    [InlineData(10d, 5d, 5d, false)]
    public void Convert_ExclusiveMaximum_BecomesABoundWithAFlag(double exclusive, double? inclusive, double bound, bool flagged)
    {
        // Act
        var converted = Convert(new AsyncApiJsonSchema { Type = SchemaType.Number, ExclusiveMaximum = exclusive, Maximum = inclusive });

        // Assert
        converted["maximum"]!.GetValue<double>().ShouldBe(bound);
        converted.ContainsKey("exclusiveMaximum").ShouldBe(flagged);
    }

    [Fact]
    public void Convert_RewritesConstExamplesAndDiscriminator()
    {
        // Arrange
        var schema = new AsyncApiJsonSchema
        {
            Type = SchemaType.Object,
            Const = new AsyncApiAny("fixed"),
            Examples = [new AsyncApiAny("first"), new AsyncApiAny("second")],
            Discriminator = "kind",
        };

        // Act
        var converted = Convert(schema);

        // Assert
        converted["enum"]!.AsArray().ShouldHaveSingleItem()!.GetValue<string>().ShouldBe("fixed");
        converted["example"]!.GetValue<string>().ShouldBe("first");
        converted.ContainsKey("examples").ShouldBeFalse();
        converted["discriminator"]!["propertyName"]!.GetValue<string>().ShouldBe("kind");
    }

    [Fact]
    public void Convert_DropsKeywordsOpenApi30Lacks()
    {
        // Arrange
        var schema = new AsyncApiJsonSchema
        {
            Type = SchemaType.Object,
            If = new AsyncApiJsonSchema(),
            Then = new AsyncApiJsonSchema(),
            Contains = new AsyncApiJsonSchema(),
            PropertyNames = new AsyncApiJsonSchema(),
            PatternProperties = new Dictionary<string, AsyncApiJsonSchema> { ["^a"] = new() },
        };

        // Act
        var converted = Convert(schema);

        // Assert
        converted.Select(pair => pair.Key).ShouldBe(["type"]);
    }

    [Fact]
    public void Convert_FalseSchema_IsNotAnythingExceptAsAdditionalProperties()
    {
        // Arrange
        var schema = new AsyncApiJsonSchema
        {
            Type = SchemaType.Object,
            AdditionalProperties = new FalseApiSchema(),
            Properties = new Dictionary<string, AsyncApiJsonSchema> { ["never"] = new FalseApiSchema() },
        };

        // Act
        var converted = Convert(schema);

        // Assert
        converted["additionalProperties"]!.GetValue<bool>().ShouldBeFalse();
        converted["properties"]!["never"]!["not"]!.AsObject().Count.ShouldBe(0);
    }

    [Fact]
    public void Convert_KeepsAnnotationsAndValidation()
    {
        // Arrange
        var schema = new AsyncApiJsonSchema
        {
            Type = SchemaType.String,
            Title = "Code",
            Description = "A code.",
            Format = "email",
            Pattern = "^[a-z]+$",
            MinLength = 1,
            MaxLength = 5,
            Default = new AsyncApiAny("abc"),
            Enum = [new AsyncApiAny("abc"), new AsyncApiAny("def")],
            ReadOnly = true,
            Deprecated = true,
            ExternalDocs = new AsyncApiExternalDocumentation { Url = new Uri("https://example.com/"), Description = "More." },
            Extensions = new Dictionary<string, ByteBard.AsyncAPI.Models.Interfaces.IAsyncApiExtension> { ["x-team"] = new AsyncApiAny("orders") },
        };

        // Act
        var converted = Convert(schema);

        // Assert
        converted["title"]!.GetValue<string>().ShouldBe("Code");
        converted["description"]!.GetValue<string>().ShouldBe("A code.");
        converted["format"]!.GetValue<string>().ShouldBe("email");
        converted["pattern"]!.GetValue<string>().ShouldBe("^[a-z]+$");
        converted["minLength"]!.GetValue<int>().ShouldBe(1);
        converted["maxLength"]!.GetValue<int>().ShouldBe(5);
        converted["default"]!.GetValue<string>().ShouldBe("abc");
        converted["enum"]!.AsArray().Count.ShouldBe(2);
        converted["readOnly"]!.GetValue<bool>().ShouldBeTrue();
        converted["deprecated"]!.GetValue<bool>().ShouldBeTrue();
        converted["externalDocs"]!["url"]!.GetValue<string>().ShouldBe("https://example.com/");
        converted["externalDocs"]!["description"]!.GetValue<string>().ShouldBe("More.");
        converted["x-team"]!.GetValue<string>().ShouldBe("orders");
    }

    [Fact]
    public void Convert_ArrayAndObjectKeywords_CarryOver()
    {
        // Arrange
        var schema = new AsyncApiJsonSchema
        {
            Type = SchemaType.Array,
            Items = new AsyncApiJsonSchema
            {
                Type = SchemaType.Object,
                Properties = new Dictionary<string, AsyncApiJsonSchema> { ["id"] = new() { Type = SchemaType.Integer } },
                Required = new HashSet<string> { "id" },
                MinProperties = 1,
                MaxProperties = 3,
                AllOf = [new AsyncApiJsonSchema { WriteOnly = true }],
                Not = new AsyncApiJsonSchema { Type = SchemaType.Null },
            },
            MinItems = 1,
            MaxItems = 10,
            UniqueItems = true,
            MultipleOf = 2,
        };

        // Act
        var converted = Convert(schema);

        // Assert
        converted["minItems"]!.GetValue<int>().ShouldBe(1);
        converted["maxItems"]!.GetValue<int>().ShouldBe(10);
        converted["uniqueItems"]!.GetValue<bool>().ShouldBeTrue();
        converted["multipleOf"]!.GetValue<double>().ShouldBe(2);
        var item = converted["items"]!;
        item["required"]!.AsArray().ShouldHaveSingleItem()!.GetValue<string>().ShouldBe("id");
        item["minProperties"]!.GetValue<int>().ShouldBe(1);
        item["maxProperties"]!.GetValue<int>().ShouldBe(3);
        item["allOf"]![0]!["writeOnly"]!.GetValue<bool>().ShouldBeTrue();
        item["not"]!["nullable"]!.GetValue<bool>().ShouldBeTrue();
    }

    [Fact]
    public void Convert_ReferenceOutsideComponents_StaysARef()
    {
        // Arrange
        var schema = new AsyncApiJsonSchemaReference("https://example.com/schemas/order.json");

        // Act
        var converted = Convert(schema);

        // Assert
        converted["$ref"]!.GetValue<string>().ShouldBe("https://example.com/schemas/order.json");
    }

    [Fact]
    public void Convert_UnresolvableComponentReference_StaysARef()
    {
        // Act
        var converted = Convert(new AsyncApiJsonSchemaReference("#/components/schemas/missing"));

        // Assert
        converted["$ref"]!.GetValue<string>().ShouldBe("#/components/schemas/missing");
    }

    private static JsonObject Convert(AsyncApiJsonSchema schema) =>
        new JsonSchemaToOpenApiConverter(new AsyncApiDocument()).Convert(schema);
}
