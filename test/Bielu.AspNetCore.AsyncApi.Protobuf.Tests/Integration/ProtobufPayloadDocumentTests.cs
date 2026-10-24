using System.Text.Json;
using Bielu.AspNetCore.AsyncApi.Protobuf.Tests.Fixtures;
using Bielu.AspNetCore.AsyncApi.Services;
using ByteBard.AsyncAPI;
using Json.Schema;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Protobuf.Tests.Integration;

public class ProtobufPayloadDocumentTests
{
    [Fact]
    public async Task AddProtobufPayloads_ReplacesProtobufPayloadsWithTheirSource()
    {
        // Act
        using var document = await ProtobufTestApp.GetDocumentAsync(options => options.AddProtobufPayloads());

        // Assert
        var payload = Message(document, "orderPlaced").GetProperty("payload");
        payload.GetProperty("schemaFormat").GetString().ShouldBe("application/vnd.google.protobuf;version=3");
        var source = payload.GetProperty("schema").GetString()!;
        source.ShouldStartWith("syntax = \"proto3\";");
        source.ShouldContain("// @RootNode\nmessage OrderPlaced {");

        Message(document, "legacyEvent").GetProperty("payload").GetProperty("schemaFormat").GetString()
            .ShouldBe("application/vnd.google.protobuf;version=2");
    }

    [Fact]
    public async Task AddProtobufPayloads_LeavesOtherPayloadsAsJsonSchema()
    {
        // Act
        using var document = await ProtobufTestApp.GetDocumentAsync(options => options.AddProtobufPayloads());

        // Assert
        Message(document, "plainEvent").GetProperty("payload").GetProperty("$ref").GetString()
            .ShouldBe("#/components/schemas/plainEvent");
    }

    [Fact]
    public async Task AddProtobufPayloads_RemovesItsMarksFromTheJsonSchemas()
    {
        // Act
        using var document = await ProtobufTestApp.GetDocumentAsync(options => options.AddProtobufPayloads());

        // Assert
        document.RootElement.GetRawText().ShouldNotContain(ProtobufPayloadTransformer.MarkerExtension);
    }

    [Fact]
    public async Task AddProtobufPayloads_WithMessageFilter_LeavesOtherMessagesAlone()
    {
        // Act
        using var document = await ProtobufTestApp.GetDocumentAsync(options =>
            options.AddProtobufPayloads(protobuf => protobuf.ForMessages("legacyEvent")));

        // Assert
        Message(document, "orderPlaced").GetProperty("payload").TryGetProperty("$ref", out _).ShouldBeTrue();
        Message(document, "legacyEvent").GetProperty("payload").TryGetProperty("schemaFormat", out _).ShouldBeTrue();
        document.RootElement.GetRawText().ShouldNotContain(ProtobufPayloadTransformer.MarkerExtension);
    }

    [Fact]
    public async Task WithoutAddProtobufPayloads_ProtobufPayloadsStayJsonSchema()
    {
        // Act
        using var document = await ProtobufTestApp.GetDocumentAsync(_ => { });

        // Assert
        Message(document, "orderPlaced").GetProperty("payload").TryGetProperty("$ref", out _).ShouldBeTrue();
    }

    [Theory]
    [InlineData(AsyncApiVersion.AsyncApi2_0)]
    [InlineData(AsyncApiVersion.AsyncApi3_0)]
    public async Task ServedDocument_ConformsToOfficialSchema(AsyncApiVersion version)
    {
        // Act
        using var document = await ProtobufTestApp.GetDocumentAsync(options =>
        {
            options.AsyncApiVersion = version;
            options.AddProtobufPayloads();
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
    public void AddProtobufPayloads_ThrowsForNullOptions()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => AsyncApiProtobufOptionsExtensions.AddProtobufPayloads(null!));
    }

    [Fact]
    public void AddProtobufPayloads_RegistersOneTransformerForSchemasAndTheDocument()
    {
        // Arrange
        var options = new AsyncApiOptions();

        // Act
        var result = options.AddProtobufPayloads();

        // Assert
        result.ShouldBeSameAs(options);
        var documentTransformer = options.DocumentTransformers.ShouldHaveSingleItem().ShouldBeOfType<ProtobufPayloadTransformer>();
        options.SchemaTransformers.ShouldHaveSingleItem().ShouldBeSameAs(documentTransformer);
    }

    [Fact]
    public void ProtobufPayloadOptions_ValidateArguments()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new ProtobufPayloadOptions().ForMessages(null!));
        Should.Throw<ArgumentException>(() => new ProtobufPayloadOptions().ForMessages("ok", ""));
    }

    private static JsonElement Message(JsonDocument document, string key) =>
        document.RootElement.GetProperty("components").GetProperty("messages").GetProperty(key);
}
