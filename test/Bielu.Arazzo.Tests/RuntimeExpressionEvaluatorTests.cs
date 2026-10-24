using System.Text.Json.Nodes;
using Bielu.Arazzo.Expressions;
using Shouldly;
using Xunit;

namespace Bielu.Arazzo.Tests;

public class RuntimeExpressionEvaluatorTests
{
    private sealed class FakeContext : IRuntimeExpressionContext
    {
        public string? Self => "https://example.com/flow.arazzo.json";

        public string? Url => "https://api.example.com/orders";

        public string? Method => "POST";

        public int? StatusCode { get; init; } = 201;

        public List<string> Calls { get; } = [];

        public JsonNode? GetRequestValue(RuntimeExpressionSource source) => Record($"request:{source.Kind}:{source.Name}:{source.JsonPointer}");

        public JsonNode? GetResponseValue(RuntimeExpressionSource source) => Record($"response:{source.Kind}:{source.Name}:{source.JsonPointer}");

        public JsonNode? GetMessageValue(RuntimeExpressionSource source) => Record($"message:{source.Kind}:{source.Name}:{source.JsonPointer}");

        public JsonNode? GetInput(string name) => JsonNode.Parse("""{"order":{"id":"ord-1","lines":[{"sku":"A"}]}}""")![name]?.DeepClone();

        public JsonNode? GetOutput(string name) => JsonValue.Create($"output:{name}");

        public JsonNode? GetStepOutput(string stepId, string outputName) => JsonNode.Parse($$"""{"step":"{{stepId}}","name":"{{outputName}}"}""");

        public JsonNode? GetWorkflowField(string workflowName, string field, string? fieldName) => JsonValue.Create($"{workflowName}:{field}:{fieldName}");

        public JsonNode? GetSourceDescriptionReference(string sourceName, string referenceId) => JsonValue.Create($"{sourceName}:{referenceId}");

        public JsonNode? GetComponent(string field, string name) => JsonValue.Create($"{field}:{name}");

        private JsonValue Record(string call)
        {
            Calls.Add(call);
            return JsonValue.Create(call);
        }
    }

    private static JsonNode? Evaluate(string expression, IRuntimeExpressionContext? context = null)
    {
        RuntimeExpressionParser.TryParse(expression, out var parsed, out var error).ShouldBeTrue(error ?? string.Empty);
        return RuntimeExpressionEvaluator.Evaluate(parsed!, context ?? new FakeContext());
    }

    [Fact]
    public void Evaluate_BareLiterals_ReadContextProperties()
    {
        // Act & Assert
        Evaluate("$url")!.GetValue<string>().ShouldBe("https://api.example.com/orders");
        Evaluate("$method")!.GetValue<string>().ShouldBe("POST");
        Evaluate("$statusCode")!.GetValue<int>().ShouldBe(201);
        Evaluate("$self")!.GetValue<string>().ShouldBe("https://example.com/flow.arazzo.json");
    }

    [Fact]
    public void Evaluate_StatusCodeWithoutResponse_ReturnsNull()
    {
        // Act & Assert
        Evaluate("$statusCode", new FakeContext { StatusCode = null }).ShouldBeNull();
    }

    [Fact]
    public void Evaluate_RequestResponseAndMessage_DelegateWithTheirSource()
    {
        // Arrange
        var context = new FakeContext();

        // Act
        Evaluate("$request.header.X-Trace", context);
        Evaluate("$response.body#/id", context);
        Evaluate("$message.payload#/level", context);

        // Assert
        context.Calls.Count.ShouldBe(3);
        context.Calls[0].ShouldStartWith("request:Header:X-Trace");
        context.Calls[1].ShouldStartWith("response:Body:");
        context.Calls[1].ShouldEndWith("/id");
        context.Calls[2].ShouldStartWith("message:Payload:");
    }

    [Fact]
    public void Evaluate_InputsWithPointer_ResolvesIntoTheValue()
    {
        // Act & Assert
        Evaluate("$inputs.order#/id")!.GetValue<string>().ShouldBe("ord-1");
        Evaluate("$inputs.order#/lines/0/sku")!.GetValue<string>().ShouldBe("A");
        Evaluate("$inputs.order")!["id"]!.GetValue<string>().ShouldBe("ord-1");
    }

    [Fact]
    public void Evaluate_PointerToMissingMember_ReturnsNull()
    {
        // Act & Assert
        Evaluate("$inputs.order#/missing").ShouldBeNull();
        Evaluate("$inputs.unknown#/id").ShouldBeNull();
    }

    [Fact]
    public void Evaluate_OutputsStepsWorkflowsSourcesAndComponents_DelegateToTheContext()
    {
        // Act & Assert
        Evaluate("$outputs.orderId")!.GetValue<string>().ShouldBe("output:orderId");
        Evaluate("$steps.create.outputs.orderId#/step")!.GetValue<string>().ShouldBe("create");
        Evaluate("$workflows.checkout.outputs.orderId")!.GetValue<string>().ShouldStartWith("checkout:outputs");
        Evaluate("$sourceDescriptions.api.pay")!.GetValue<string>().ShouldBe("api:pay");
        Evaluate("$components.parameters.page")!.GetValue<string>().ShouldBe("parameters:page");
    }

    [Fact]
    public void Evaluate_NullArguments_Throw()
    {
        // Arrange
        RuntimeExpressionParser.TryParse("$url", out var parsed, out _);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => RuntimeExpressionEvaluator.Evaluate(null!, new FakeContext()));
        Should.Throw<ArgumentNullException>(() => RuntimeExpressionEvaluator.Evaluate(parsed!, null!));
    }
}
