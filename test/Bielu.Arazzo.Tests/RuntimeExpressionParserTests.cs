using Bielu.Arazzo.Expressions;
using Shouldly;
using Xunit;

namespace Bielu.Arazzo.Tests;

public class RuntimeExpressionParserTests
{
    [Theory]
    [InlineData("$url")]
    [InlineData("$method")]
    [InlineData("$statusCode")]
    [InlineData("$self")]
    public void ParsesBareLiterals(string input)
    {
        RuntimeExpressionParser.TryParse(input, out var expression, out var error).ShouldBeTrue(error ?? string.Empty);
        expression!.Raw.ShouldBe(input);
    }

    [Fact]
    public void ParsesRequestHeader()
    {
        RuntimeExpressionParser.TryParse("$request.header.accept", out var expression, out var error).ShouldBeTrue(error ?? string.Empty);
        var request = expression.ShouldBeOfType<RuntimeExpression.Request>();
        request.Source.Kind.ShouldBe(RuntimeExpressionSourceKind.Header);
        request.Source.Name.ShouldBe("accept");
    }

    [Fact]
    public void ParsesRequestPath()
    {
        RuntimeExpressionParser.TryParse("$request.path.id", out var expression, out var error).ShouldBeTrue(error ?? string.Empty);
        var request = expression.ShouldBeOfType<RuntimeExpression.Request>();
        request.Source.Kind.ShouldBe(RuntimeExpressionSourceKind.Path);
        request.Source.Name.ShouldBe("id");
    }

    [Fact]
    public void ParsesRequestBodyWithPointer()
    {
        RuntimeExpressionParser.TryParse("$request.body#/user/uuid", out var expression, out var error).ShouldBeTrue(error ?? string.Empty);
        var request = expression.ShouldBeOfType<RuntimeExpression.Request>();
        request.Source.Kind.ShouldBe(RuntimeExpressionSourceKind.Body);
        request.Source.JsonPointer.ShouldBe("/user/uuid");
    }

    [Fact]
    public void ParsesResponseBodyArrayIndexPointer()
    {
        RuntimeExpressionParser.TryParse("$response.body#/items/0/id", out var expression, out var error).ShouldBeTrue(error ?? string.Empty);
        var response = expression.ShouldBeOfType<RuntimeExpression.Response>();
        response.Source.JsonPointer.ShouldBe("/items/0/id");
    }

    [Fact]
    public void ParsesResponseHeader()
    {
        RuntimeExpressionParser.TryParse("$response.header.Server", out var expression, out var error).ShouldBeTrue(error ?? string.Empty);
        var response = expression.ShouldBeOfType<RuntimeExpression.Response>();
        response.Source.Kind.ShouldBe(RuntimeExpressionSourceKind.Header);
        response.Source.Name.ShouldBe("Server");
    }

    [Fact]
    public void ParsesMessageHeader()
    {
        RuntimeExpressionParser.TryParse("$message.header.Server", out var expression, out var error).ShouldBeTrue(error ?? string.Empty);
        var message = expression.ShouldBeOfType<RuntimeExpression.Message>();
        message.Source.Kind.ShouldBe(RuntimeExpressionSourceKind.Header);
    }

    [Fact]
    public void ParsesMessagePayloadWithPointer()
    {
        RuntimeExpressionParser.TryParse("$message.payload#/status", out var expression, out var error).ShouldBeTrue(error ?? string.Empty);
        var message = expression.ShouldBeOfType<RuntimeExpression.Message>();
        message.Source.Kind.ShouldBe(RuntimeExpressionSourceKind.Payload);
        message.Source.JsonPointer.ShouldBe("/status");
    }

    [Fact]
    public void ParsesInputs()
    {
        RuntimeExpressionParser.TryParse("$inputs.username", out var expression, out var error).ShouldBeTrue(error ?? string.Empty);
        var inputs = expression.ShouldBeOfType<RuntimeExpression.Inputs>();
        inputs.Name.ShouldBe("username");
        inputs.JsonPointer.ShouldBeNull();
    }

    [Fact]
    public void ParsesWorkflowsInputsField()
    {
        RuntimeExpressionParser.TryParse("$workflows.foo.inputs.username", out var expression, out var error).ShouldBeTrue(error ?? string.Empty);
        var workflows = expression.ShouldBeOfType<RuntimeExpression.Workflows>();
        workflows.WorkflowName.ShouldBe("foo");
        workflows.Field.ShouldBe("inputs");
        workflows.FieldName.ShouldBe("username");
    }

    [Fact]
    public void ParsesStepsOutputs()
    {
        RuntimeExpressionParser.TryParse("$steps.loginStep.outputs.tokenExpires", out var expression, out var error).ShouldBeTrue(error ?? string.Empty);
        var steps = expression.ShouldBeOfType<RuntimeExpression.Steps>();
        steps.StepId.ShouldBe("loginStep");
        steps.OutputName.ShouldBe("tokenExpires");
    }

    [Fact]
    public void ParsesSourceDescriptionsOperationId()
    {
        RuntimeExpressionParser.TryParse("$sourceDescriptions.petstoreDescription.loginUser", out var expression, out var error).ShouldBeTrue(error ?? string.Empty);
        var sourceDescriptions = expression.ShouldBeOfType<RuntimeExpression.SourceDescriptions>();
        sourceDescriptions.SourceName.ShouldBe("petstoreDescription");
        sourceDescriptions.ReferenceId.ShouldBe("loginUser");
    }

    [Fact]
    public void SourceDescriptionsReferenceIdMayContainDots()
    {
        // operationIds have no character restrictions per spec §5.9 — must not be dot-split further.
        RuntimeExpressionParser.TryParse("$sourceDescriptions.events.com.example.sendLightMeasurement", out var expression, out var error).ShouldBeTrue(error ?? string.Empty);
        var sourceDescriptions = expression.ShouldBeOfType<RuntimeExpression.SourceDescriptions>();
        sourceDescriptions.SourceName.ShouldBe("events");
        sourceDescriptions.ReferenceId.ShouldBe("com.example.sendLightMeasurement");
    }

    [Fact]
    public void ParsesComponentsSuccessAction()
    {
        RuntimeExpressionParser.TryParse("$components.successActions.notify", out var expression, out var error).ShouldBeTrue(error ?? string.Empty);
        var components = expression.ShouldBeOfType<RuntimeExpression.Components>();
        components.Field.ShouldBe("successActions");
        components.Name.ShouldBe("notify");
    }

    [Fact]
    public void ParsesComponentsParameter()
    {
        RuntimeExpressionParser.TryParse("$components.parameters.page", out var expression, out var error).ShouldBeTrue(error ?? string.Empty);
        var components = expression.ShouldBeOfType<RuntimeExpression.Components>();
        components.Field.ShouldBe("parameters");
        components.Name.ShouldBe("page");
    }

    [Theory]
    [InlineData("url")]
    [InlineData("")]
    [InlineData("$bogus")]
    [InlineData("$steps.loginStep.tokenExpires")]
    [InlineData("$request.unknown.thing")]
    public void RejectsInvalidExpressions(string input)
    {
        RuntimeExpressionParser.TryParse(input, out var expression, out var error).ShouldBeFalse();
        expression.ShouldBeNull();
        error.ShouldNotBeNull();
    }
}
