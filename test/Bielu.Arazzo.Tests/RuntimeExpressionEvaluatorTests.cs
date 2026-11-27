using System.Text.Json.Nodes;
using Bielu.Arazzo.Expressions;
using Shouldly;
using Xunit;

namespace Bielu.Arazzo.Tests;

public class RuntimeExpressionEvaluatorTests
{
    private static RuntimeExpression Parse(string input)
    {
        RuntimeExpressionParser.TryParse(input, out var expression, out var error).ShouldBeTrue(error);
        return expression!;
    }

    private static JsonNode? Evaluate(string input, FakeContext? context = null) =>
        RuntimeExpressionEvaluator.Evaluate(Parse(input), context ?? new FakeContext());

    [Fact]
    public void Evaluate_RequestScalars_ReturnTheContextValues()
    {
        Evaluate("$url")!.GetValue<string>().ShouldBe("https://api.example.com/orders");
        Evaluate("$method")!.GetValue<string>().ShouldBe("POST");
        Evaluate("$statusCode")!.GetValue<int>().ShouldBe(201);
        Evaluate("$self")!.GetValue<string>().ShouldBe("https://example.com/checkout.arazzo.yaml");
    }

    [Fact]
    public void Evaluate_StatusCodeBeforeAResponse_IsNull()
    {
        Evaluate("$statusCode", new FakeContext { StatusCode = null }).ShouldBeNull();
    }

    [Theory]
    [InlineData("$request.header.X-Trace", "request:Header:X-Trace:")]
    [InlineData("$response.body#/id", "response:Body::/id")]
    [InlineData("$message.payload#/orderId", "message:Payload::/orderId")]
    [InlineData("$request.query.page", "request:Query:page:")]
    [InlineData("$response.path.orderId", "response:Path:orderId:")]
    public void Evaluate_RequestResponseAndMessageSources_AreHandedToTheContext(string input, string expected)
    {
        Evaluate(input)!.GetValue<string>().ShouldBe(expected);
    }

    [Theory]
    [InlineData("$inputs.order", """{"id":7,"lines":[{"sku":"a"}]}""")]
    [InlineData("$inputs.order#/id", "7")]
    [InlineData("$inputs.order#/lines/0/sku", "\"a\"")]
    [InlineData("$outputs.total#/amount", "12.5")]
    [InlineData("$steps.place-order.outputs.result#/status", "\"placed\"")]
    [InlineData("$workflows.refund.outputs.refundId", "\"r-1\"")]
    public void Evaluate_NamedValuesWithPointers_ResolveTheirPointer(string input, string expectedJson)
    {
        Evaluate(input)!.ToJsonString().ShouldBe(expectedJson);
    }

    [Theory]
    [InlineData("$inputs.order#/missing")]
    [InlineData("$inputs.unknown#/id")]
    [InlineData("$inputs.unknown")]
    public void Evaluate_PointerOrNameThatResolvesToNothing_IsNull(string input)
    {
        Evaluate(input).ShouldBeNull();
    }

    [Fact]
    public void Evaluate_SourceDescriptionAndComponent_AreHandedToTheContext()
    {
        Evaluate("$sourceDescriptions.petstore.getPet")!.GetValue<string>().ShouldBe("source:petstore:getPet");
        Evaluate("$components.parameters.page")!.GetValue<string>().ShouldBe("component:parameters:page");
    }

    [Fact]
    public void Evaluate_NullArguments_Throw()
    {
        Should.Throw<ArgumentNullException>(() => RuntimeExpressionEvaluator.Evaluate(null!, new FakeContext()));
        Should.Throw<ArgumentNullException>(() => RuntimeExpressionEvaluator.Evaluate(Parse("$url"), null!));
    }

    [Theory]
    [InlineData("$inputs.", "missing a name")]
    [InlineData("$inputs.bad name", "invalid name")]
    [InlineData("$outputs.total#bad", "")]
    [InlineData("$steps.a b.outputs.x", "invalid stepId")]
    [InlineData("$steps.a.outputs.x y", "invalid outputName")]
    [InlineData("$steps.a.outputs.x#bad", "")]
    [InlineData("$workflows.refund", "must be in the form")]
    [InlineData("$workflows.re fund.outputs.x", "invalid workflowName")]
    [InlineData("$workflows.refund.inputs.x y", "invalid fieldName")]
    [InlineData("$workflows.refund.inputs.x#bad", "")]
    [InlineData("$sourceDescriptions.petstore", "missing a source name and reference id")]
    [InlineData("$sourceDescriptions..getPet", "missing a source name or reference id")]
    [InlineData("$sourceDescriptions.pet store.getPet", "invalid source name")]
    [InlineData("$components.parameters", "missing a components field and name")]
    [InlineData("$components.parameters.", "missing a components field or name")]
    [InlineData("$components.schemas.Order", "unrecognized components field")]
    [InlineData("$components.parameters.bad name", "invalid component name")]
    [InlineData("$request.body#bad", "")]
    [InlineData("$message.payload#bad", "")]
    [InlineData("$response.cookie.session", "unrecognized source")]
    public void TryParse_MalformedExpression_FailsWithAnError(string input, string expectedError)
    {
        // Act
        var parsed = RuntimeExpressionParser.TryParse(input, out var expression, out var error);

        // Assert
        parsed.ShouldBeFalse();
        expression.ShouldBeNull();
        error.ShouldNotBeNullOrEmpty();
        error.ShouldContain(expectedError);
    }

    private sealed class FakeContext : IRuntimeExpressionContext
    {
        public string? Self => "https://example.com/checkout.arazzo.yaml";

        public string? Url => "https://api.example.com/orders";

        public string? Method => "POST";

        public int? StatusCode { get; init; } = 201;

        public JsonNode? GetRequestValue(RuntimeExpressionSource source) => Describe("request", source);

        public JsonNode? GetResponseValue(RuntimeExpressionSource source) => Describe("response", source);

        public JsonNode? GetMessageValue(RuntimeExpressionSource source) => Describe("message", source);

        public JsonNode? GetInput(string name) =>
            name == "order" ? JsonNode.Parse("""{"id":7,"lines":[{"sku":"a"}]}""") : null;

        public JsonNode? GetOutput(string name) => name == "total" ? JsonNode.Parse("""{"amount":12.5}""") : null;

        public JsonNode? GetStepOutput(string stepId, string outputName) =>
            stepId == "place-order" && outputName == "result" ? JsonNode.Parse("""{"status":"placed"}""") : null;

        public JsonNode? GetWorkflowField(string workflowName, string field, string? fieldName) =>
            workflowName == "refund" && field == "outputs" && fieldName == "refundId" ? JsonValue.Create("r-1") : null;

        public JsonNode? GetSourceDescriptionReference(string sourceName, string referenceId) =>
            JsonValue.Create($"source:{sourceName}:{referenceId}");

        public JsonNode? GetComponent(string field, string name) => JsonValue.Create($"component:{field}:{name}");

        private static JsonNode Describe(string side, RuntimeExpressionSource source) =>
            JsonValue.Create($"{side}:{source.Kind}:{source.Name}:{source.JsonPointer}");
    }
}
