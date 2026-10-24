using System.Text.Json;
using Bielu.AspNetCore.AsyncApi.Avro.Tests.Fixtures;
using Bielu.AspNetCore.AsyncApi.Services;
using ByteBard.AsyncAPI;
using Json.Schema;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Avro.Tests.Integration;

public class AvroPayloadDocumentTests
{
    [Fact]
    public async Task AddAvroPayloads_ReplacesThePayloadWithAnAvroRecord()
    {
        // Act
        using var document = await AvroTestApp.GetDocumentAsync(options =>
            options.AddAvroPayloads(avro => avro.WithNamespace("com.example.orders")));

        // Assert
        var payload = Message(document, "orderPlaced").GetProperty("payload");
        payload.GetProperty("schemaFormat").GetString().ShouldBe("application/vnd.apache.avro;version=1.9.0");
        var record = payload.GetProperty("schema");
        record.GetProperty("type").GetString().ShouldBe("record");
        record.GetProperty("name").GetString().ShouldBe("OrderPlaced");
        record.GetProperty("namespace").GetString().ShouldBe("com.example.orders");

        var fields = record.GetProperty("fields").EnumerateArray().ToDictionary(f => f.GetProperty("name").GetString()!, f => f);
        fields["orderId"].GetProperty("type").GetProperty("logicalType").GetString().ShouldBe("uuid");
        fields["placedAt"].GetProperty("type").GetProperty("logicalType").GetString().ShouldBe("timestamp-millis");
        fields["deliveryDate"].GetProperty("type").GetProperty("logicalType").GetString().ShouldBe("date");
        fields["total"].GetProperty("type").GetString().ShouldBe("long");
        fields["weight"].GetProperty("type").GetString().ShouldBe("double");
        fields["express"].GetProperty("type").GetString().ShouldBe("boolean");
        fields["note"].GetProperty("type").EnumerateArray().Select(t => t.GetString()).ShouldBe(["null", "string"]);
        fields["discount"].GetProperty("type").EnumerateArray().Select(t => t.GetString()).ShouldBe(["null", "int"]);
        fields["signature"].GetProperty("type").GetString().ShouldBe("bytes");
        fields["attributes"].GetProperty("type").GetProperty("type").GetString().ShouldBe("map");
        var lines = fields["lines"].GetProperty("type");
        lines.GetProperty("type").GetString().ShouldBe("array");
        lines.GetProperty("items").GetProperty("type").GetString().ShouldBe("record");
        lines.GetProperty("items").GetProperty("fields")[1].GetProperty("type").GetString().ShouldBe("int");
    }

    [Fact]
    public async Task AddAvroPayloads_KeepsTheJsonSchemaComponents()
    {
        // Act
        using var document = await AvroTestApp.GetDocumentAsync(options => options.AddAvroPayloads());

        // Assert
        document.RootElement.GetProperty("components").GetProperty("schemas").TryGetProperty("orderPlaced", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task AddAvroPayloads_WithMessageFilter_LeavesOtherMessagesAsJsonSchema()
    {
        // Act
        using var document = await AvroTestApp.GetDocumentAsync(options =>
            options.AddAvroPayloads(avro => avro.ForMessages("orderPlaced")));

        // Assert
        Message(document, "orderPlaced").GetProperty("payload").TryGetProperty("schemaFormat", out _).ShouldBeTrue();
        Message(document, "orderAudited").GetProperty("payload").GetProperty("$ref").GetString()
            .ShouldBe("#/components/schemas/orderAudited");
    }

    [Fact]
    public async Task AddAvroPayloads_WithAvroVersion_DeclaresIt()
    {
        // Act
        using var document = await AvroTestApp.GetDocumentAsync(options =>
            options.AddAvroPayloads(avro => avro.AvroVersion = "1.11.1"));

        // Assert
        Message(document, "orderAudited").GetProperty("payload").GetProperty("schemaFormat").GetString()
            .ShouldBe("application/vnd.apache.avro;version=1.11.1");
    }

    [Theory]
    [InlineData(AsyncApiVersion.AsyncApi2_0)]
    [InlineData(AsyncApiVersion.AsyncApi3_0)]
    public async Task ServedDocument_ConformsToOfficialSchema(AsyncApiVersion version)
    {
        // Act
        using var document = await AvroTestApp.GetDocumentAsync(options =>
        {
            options.AsyncApiVersion = version;
            options.AddAvroPayloads(avro => avro.WithNamespace("com.example.orders"));
        });

        // Assert
        var declaredVersion = document.RootElement.GetProperty("asyncapi").GetString()!;
        var schemaPath = Path.Combine(AppContext.BaseDirectory, "Conformance", "asyncapi", $"{declaredVersion}.json");
        var result = JsonSchema.FromText(File.ReadAllText(schemaPath))
            .Evaluate(document.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });
        result.IsValid.ShouldBeTrue(string.Join(Environment.NewLine, (result.Details ?? [])
            .Where(detail => detail.Errors is { Count: > 0 })
            .SelectMany(detail => detail.Errors!.Select(error => $"{detail.InstanceLocation} {error.Value}"))));
    }

    [Fact]
    public async Task AddAvroPayloads_AsyncApi2_PutsTheSchemaFormatOnTheMessage()
    {
        // Act
        using var document = await AvroTestApp.GetDocumentAsync(options =>
        {
            options.AsyncApiVersion = AsyncApiVersion.AsyncApi2_0;
            options.AddAvroPayloads();
        });

        // Assert
        var message = Message(document, "orderAudited");
        message.GetProperty("schemaFormat").GetString().ShouldBe("application/vnd.apache.avro;version=1.9.0");
        message.GetProperty("payload").GetProperty("type").GetString().ShouldBe("record");
    }

    [Fact]
    public void AddAvroPayloads_ThrowsForNullOptions()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => AsyncApiAvroOptionsExtensions.AddAvroPayloads(null!));
    }

    [Fact]
    public void AddAvroPayloads_ThrowsForEmptyVersion()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => new AsyncApiOptions().AddAvroPayloads(avro => avro.AvroVersion = " "));
    }

    [Fact]
    public void AddAvroPayloads_RegistersADocumentTransformer()
    {
        // Arrange
        var options = new AsyncApiOptions();

        // Act
        var result = options.AddAvroPayloads();

        // Assert
        result.ShouldBeSameAs(options);
        options.DocumentTransformers.ShouldHaveSingleItem().ShouldBeOfType<AvroPayloadDocumentTransformer>();
    }

    [Fact]
    public void AvroPayloadOptions_ValidateArguments()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => new AvroPayloadOptions().WithNamespace(""));
        Should.Throw<ArgumentNullException>(() => new AvroPayloadOptions().ForMessages(null!));
        Should.Throw<ArgumentException>(() => new AvroPayloadOptions().ForMessages("ok", " "));
    }

    private static JsonElement Message(JsonDocument document, string key) =>
        document.RootElement.GetProperty("components").GetProperty("messages").GetProperty(key);
}
