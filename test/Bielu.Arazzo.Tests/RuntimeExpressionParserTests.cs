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
    public void TryParse_BareLiteral_ReturnsExpression(string input)
    {
        RuntimeExpressionParser.TryParse(input, out var expression, out var error).ShouldBeTrue(error ?? string.Empty);
        expression!.Raw.ShouldBe(input);
    }

    [Fact]
    public void TryParse_NullInput_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => RuntimeExpressionParser.TryParse(null!, out _, out _));
    }

    [Fact]
    public void TryParse_RequestHeader_ReturnsRequestExpression()
    {
        RuntimeExpressionParser.TryParse("$request.header.accept", out var expression, out var error).ShouldBeTrue(error ?? string.Empty);
        var request = expression.ShouldBeOfType<RuntimeExpression.Request>();
        request.Source.Kind.ShouldBe(RuntimeExpressionSourceKind.Header);
        request.Source.Name.ShouldBe("accept");
    }

    [Fact]
    public void TryParse_RequestPath_ReturnsRequestExpression()
    {
        RuntimeExpressionParser.TryParse("$request.path.id", out var expression, out var error).ShouldBeTrue(error ?? string.Empty);
        var request = expression.ShouldBeOfType<RuntimeExpression.Request>();
        request.Source.Kind.ShouldBe(RuntimeExpressionSourceKind.Path);
        request.Source.Name.ShouldBe("id");
    }

    [Fact]
    public void TryParse_RequestBodyWithPointer_ReturnsRequestExpression()
    {
        RuntimeExpressionParser.TryParse("$request.body#/user/uuid", out var expression, out var error).ShouldBeTrue(error ?? string.Empty);
        var request = expression.ShouldBeOfType<RuntimeExpression.Request>();
        request.Source.Kind.ShouldBe(RuntimeExpressionSourceKind.Body);
        request.Source.JsonPointer.ShouldBe("/user/uuid");
    }

    [Fact]
    public void TryParse_ResponseBodyArrayIndexPointer_ReturnsResponseExpression()
    {
        RuntimeExpressionParser.TryParse("$response.body#/items/0/id", out var expression, out var error).ShouldBeTrue(error ?? string.Empty);
        var response = expression.ShouldBeOfType<RuntimeExpression.Response>();
        response.Source.JsonPointer.ShouldBe("/items/0/id");
    }

    [Fact]
    public void TryParse_ResponseHeader_ReturnsResponseExpression()
    {
        RuntimeExpressionParser.TryParse("$response.header.Server", out var expression, out var error).ShouldBeTrue(error ?? string.Empty);
        var response = expression.ShouldBeOfType<RuntimeExpression.Response>();
        response.Source.Kind.ShouldBe(RuntimeExpressionSourceKind.Header);
        response.Source.Name.ShouldBe("Server");
    }

    [Fact]
    public void TryParse_MessageHeader_ReturnsMessageExpression()
    {
        RuntimeExpressionParser.TryParse("$message.header.Server", out var expression, out var error).ShouldBeTrue(error ?? string.Empty);
        var message = expression.ShouldBeOfType<RuntimeExpression.Message>();
        message.Source.Kind.ShouldBe(RuntimeExpressionSourceKind.Header);
    }

    [Fact]
    public void TryParse_MessagePayloadWithPointer_ReturnsMessageExpression()
    {
        RuntimeExpressionParser.TryParse("$message.payload#/status", out var expression, out var error).ShouldBeTrue(error ?? string.Empty);
        var message = expression.ShouldBeOfType<RuntimeExpression.Message>();
        message.Source.Kind.ShouldBe(RuntimeExpressionSourceKind.Payload);
        message.Source.JsonPointer.ShouldBe("/status");
    }

    [Theory]
    [InlineData("$request.body#bad-pointer")]
    [InlineData("$response.body#bad-pointer")]
    [InlineData("$message.payload#bad-pointer")]
    public void TryParse_SourceBodyOrPayloadWithMalformedJsonPointer_ReturnsFalse(string input)
    {
        RuntimeExpressionParser.TryParse(input, out var expression, out var error).ShouldBeFalse();
        expression.ShouldBeNull();
        error.ShouldNotBeNull();
    }

    [Fact]
    public void TryParse_Inputs_ReturnsInputsExpression()
    {
        RuntimeExpressionParser.TryParse("$inputs.username", out var expression, out var error).ShouldBeTrue(error ?? string.Empty);
        var inputs = expression.ShouldBeOfType<RuntimeExpression.Inputs>();
        inputs.Name.ShouldBe("username");
        inputs.JsonPointer.ShouldBeNull();
    }

    [Fact]
    public void TryParse_InputsWithMalformedJsonPointer_ReturnsFalse()
    {
        RuntimeExpressionParser.TryParse("$inputs.username#bad-pointer", out var expression, out var error).ShouldBeFalse();
        expression.ShouldBeNull();
        error.ShouldNotBeNull();
    }

    [Fact]
    public void TryParse_WorkflowsInputsField_ReturnsWorkflowsExpression()
    {
        RuntimeExpressionParser.TryParse("$workflows.foo.inputs.username", out var expression, out var error).ShouldBeTrue(error ?? string.Empty);
        var workflows = expression.ShouldBeOfType<RuntimeExpression.Workflows>();
        workflows.WorkflowName.ShouldBe("foo");
        workflows.Field.ShouldBe("inputs");
        workflows.FieldName.ShouldBe("username");
    }

    [Fact]
    public void TryParse_WorkflowsWithoutFieldName_ReturnsFalse()
    {
        RuntimeExpressionParser.TryParse("$workflows.foo.inputs", out var expression, out var error).ShouldBeFalse();
        expression.ShouldBeNull();
        error.ShouldNotBeNull();
    }

    [Fact]
    public void TryParse_StepsOutputs_ReturnsStepsExpression()
    {
        RuntimeExpressionParser.TryParse("$steps.loginStep.outputs.tokenExpires", out var expression, out var error).ShouldBeTrue(error ?? string.Empty);
        var steps = expression.ShouldBeOfType<RuntimeExpression.Steps>();
        steps.StepId.ShouldBe("loginStep");
        steps.OutputName.ShouldBe("tokenExpires");
    }

    [Fact]
    public void TryParse_StepsWithInvalidStepId_ReturnsFalse()
    {
        RuntimeExpressionParser.TryParse("$steps.login@Step.outputs.tokenExpires", out var expression, out var error).ShouldBeFalse();
        expression.ShouldBeNull();
        error.ShouldNotBeNull();
    }

    [Fact]
    public void TryParse_SourceDescriptionsOperationId_ReturnsSourceDescriptionsExpression()
    {
        RuntimeExpressionParser.TryParse("$sourceDescriptions.petstoreDescription.loginUser", out var expression, out var error).ShouldBeTrue(error ?? string.Empty);
        var sourceDescriptions = expression.ShouldBeOfType<RuntimeExpression.SourceDescriptions>();
        sourceDescriptions.SourceName.ShouldBe("petstoreDescription");
        sourceDescriptions.ReferenceId.ShouldBe("loginUser");
    }

    [Fact]
    public void TryParse_SourceDescriptionsReferenceIdWithDots_ReturnsSourceDescriptionsExpression()
    {
        // operationIds have no character restrictions per spec §5.9 — must not be dot-split further.
        RuntimeExpressionParser.TryParse("$sourceDescriptions.events.com.example.sendLightMeasurement", out var expression, out var error).ShouldBeTrue(error ?? string.Empty);
        var sourceDescriptions = expression.ShouldBeOfType<RuntimeExpression.SourceDescriptions>();
        sourceDescriptions.SourceName.ShouldBe("events");
        sourceDescriptions.ReferenceId.ShouldBe("com.example.sendLightMeasurement");
    }

    [Fact]
    public void TryParse_ComponentsSuccessAction_ReturnsComponentsExpression()
    {
        RuntimeExpressionParser.TryParse("$components.successActions.notify", out var expression, out var error).ShouldBeTrue(error ?? string.Empty);
        var components = expression.ShouldBeOfType<RuntimeExpression.Components>();
        components.Field.ShouldBe("successActions");
        components.Name.ShouldBe("notify");
    }

    [Fact]
    public void TryParse_ComponentsParameter_ReturnsComponentsExpression()
    {
        RuntimeExpressionParser.TryParse("$components.parameters.page", out var expression, out var error).ShouldBeTrue(error ?? string.Empty);
        var components = expression.ShouldBeOfType<RuntimeExpression.Components>();
        components.Field.ShouldBe("parameters");
        components.Name.ShouldBe("page");
    }

    [Fact]
    public void TryParse_ComponentsWithDisallowedField_ReturnsFalse()
    {
        RuntimeExpressionParser.TryParse("$components.inputs.foo", out var expression, out var error).ShouldBeFalse();
        expression.ShouldBeNull();
        error.ShouldNotBeNull();
    }

    [Theory]
    [InlineData("url")]
    [InlineData("")]
    [InlineData("$bogus")]
    [InlineData("$steps.loginStep.tokenExpires")]
    [InlineData("$request.unknown.thing")]
    public void TryParse_InvalidExpression_ReturnsFalse(string input)
    {
        RuntimeExpressionParser.TryParse(input, out var expression, out var error).ShouldBeFalse();
        expression.ShouldBeNull();
        error.ShouldNotBeNull();
    }
}
