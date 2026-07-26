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
        // Arrange

        // Act
        var result = RuntimeExpressionParser.TryParse(input, out var expression, out var error);

        // Assert
        result.ShouldBeTrue(error ?? string.Empty);
        expression!.Raw.ShouldBe(input);
    }

    [Fact]
    public void TryParse_NullInput_ThrowsArgumentNullException()
    {
        // Arrange
        var action = () => { _ = RuntimeExpressionParser.TryParse(null!, out _, out _); };

        // Act
        var exception = Record.Exception(action);

        // Assert
        exception.ShouldBeOfType<ArgumentNullException>();
    }

    [Fact]
    public void TryParse_RequestHeader_ReturnsRequestExpression()
    {
        // Arrange
        const string input = "$request.header.accept";

        // Act
        var result = RuntimeExpressionParser.TryParse(input, out var expression, out var error);

        // Assert
        result.ShouldBeTrue(error ?? string.Empty);
        var request = expression.ShouldBeOfType<RuntimeExpression.Request>();
        request.Source.Kind.ShouldBe(RuntimeExpressionSourceKind.Header);
        request.Source.Name.ShouldBe("accept");
    }

    [Fact]
    public void TryParse_RequestPath_ReturnsRequestExpression()
    {
        // Arrange
        const string input = "$request.path.id";

        // Act
        var result = RuntimeExpressionParser.TryParse(input, out var expression, out var error);

        // Assert
        result.ShouldBeTrue(error ?? string.Empty);
        var request = expression.ShouldBeOfType<RuntimeExpression.Request>();
        request.Source.Kind.ShouldBe(RuntimeExpressionSourceKind.Path);
        request.Source.Name.ShouldBe("id");
    }

    [Fact]
    public void TryParse_RequestBodyWithPointer_ReturnsRequestExpression()
    {
        // Arrange
        const string input = "$request.body#/user/uuid";

        // Act
        var result = RuntimeExpressionParser.TryParse(input, out var expression, out var error);

        // Assert
        result.ShouldBeTrue(error ?? string.Empty);
        var request = expression.ShouldBeOfType<RuntimeExpression.Request>();
        request.Source.Kind.ShouldBe(RuntimeExpressionSourceKind.Body);
        request.Source.JsonPointer.ShouldBe("/user/uuid");
    }

    [Fact]
    public void TryParse_ResponseBodyArrayIndexPointer_ReturnsResponseExpression()
    {
        // Arrange
        const string input = "$response.body#/items/0/id";

        // Act
        var result = RuntimeExpressionParser.TryParse(input, out var expression, out var error);

        // Assert
        result.ShouldBeTrue(error ?? string.Empty);
        var response = expression.ShouldBeOfType<RuntimeExpression.Response>();
        response.Source.JsonPointer.ShouldBe("/items/0/id");
    }

    [Fact]
    public void TryParse_ResponseHeader_ReturnsResponseExpression()
    {
        // Arrange
        const string input = "$response.header.Server";

        // Act
        var result = RuntimeExpressionParser.TryParse(input, out var expression, out var error);

        // Assert
        result.ShouldBeTrue(error ?? string.Empty);
        var response = expression.ShouldBeOfType<RuntimeExpression.Response>();
        response.Source.Kind.ShouldBe(RuntimeExpressionSourceKind.Header);
        response.Source.Name.ShouldBe("Server");
    }

    [Fact]
    public void TryParse_MessageHeader_ReturnsMessageExpression()
    {
        // Arrange
        const string input = "$message.header.Server";

        // Act
        var result = RuntimeExpressionParser.TryParse(input, out var expression, out var error);

        // Assert
        result.ShouldBeTrue(error ?? string.Empty);
        var message = expression.ShouldBeOfType<RuntimeExpression.Message>();
        message.Source.Kind.ShouldBe(RuntimeExpressionSourceKind.Header);
    }

    [Fact]
    public void TryParse_MessagePayloadWithPointer_ReturnsMessageExpression()
    {
        // Arrange
        const string input = "$message.payload#/status";

        // Act
        var result = RuntimeExpressionParser.TryParse(input, out var expression, out var error);

        // Assert
        result.ShouldBeTrue(error ?? string.Empty);
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
        // Arrange

        // Act
        var result = RuntimeExpressionParser.TryParse(input, out var expression, out var error);

        // Assert
        result.ShouldBeFalse();
        expression.ShouldBeNull();
        error.ShouldNotBeNull();
    }

    [Fact]
    public void TryParse_Inputs_ReturnsInputsExpression()
    {
        // Arrange
        const string input = "$inputs.username";

        // Act
        var result = RuntimeExpressionParser.TryParse(input, out var expression, out var error);

        // Assert
        result.ShouldBeTrue(error ?? string.Empty);
        var inputs = expression.ShouldBeOfType<RuntimeExpression.Inputs>();
        inputs.Name.ShouldBe("username");
        inputs.JsonPointer.ShouldBeNull();
    }

    [Fact]
    public void TryParse_InputsWithMalformedJsonPointer_ReturnsFalse()
    {
        // Arrange
        const string input = "$inputs.username#bad-pointer";

        // Act
        var result = RuntimeExpressionParser.TryParse(input, out var expression, out var error);

        // Assert
        result.ShouldBeFalse();
        expression.ShouldBeNull();
        error.ShouldNotBeNull();
    }

    [Fact]
    public void TryParse_WorkflowsInputsField_ReturnsWorkflowsExpression()
    {
        // Arrange
        const string input = "$workflows.foo.inputs.username";

        // Act
        var result = RuntimeExpressionParser.TryParse(input, out var expression, out var error);

        // Assert
        result.ShouldBeTrue(error ?? string.Empty);
        var workflows = expression.ShouldBeOfType<RuntimeExpression.Workflows>();
        workflows.WorkflowName.ShouldBe("foo");
        workflows.Field.ShouldBe("inputs");
        workflows.FieldName.ShouldBe("username");
    }

    [Fact]
    public void TryParse_WorkflowsWithoutFieldName_ReturnsFalse()
    {
        // Arrange
        const string input = "$workflows.foo.inputs";

        // Act
        var result = RuntimeExpressionParser.TryParse(input, out var expression, out var error);

        // Assert
        result.ShouldBeFalse();
        expression.ShouldBeNull();
        error.ShouldNotBeNull();
    }

    [Fact]
    public void TryParse_StepsOutputs_ReturnsStepsExpression()
    {
        // Arrange
        const string input = "$steps.loginStep.outputs.tokenExpires";

        // Act
        var result = RuntimeExpressionParser.TryParse(input, out var expression, out var error);

        // Assert
        result.ShouldBeTrue(error ?? string.Empty);
        var steps = expression.ShouldBeOfType<RuntimeExpression.Steps>();
        steps.StepId.ShouldBe("loginStep");
        steps.OutputName.ShouldBe("tokenExpires");
    }

    [Fact]
    public void TryParse_StepsWithInvalidStepId_ReturnsFalse()
    {
        // Arrange
        const string input = "$steps.login@Step.outputs.tokenExpires";

        // Act
        var result = RuntimeExpressionParser.TryParse(input, out var expression, out var error);

        // Assert
        result.ShouldBeFalse();
        expression.ShouldBeNull();
        error.ShouldNotBeNull();
    }

    [Fact]
    public void TryParse_SourceDescriptionsOperationId_ReturnsSourceDescriptionsExpression()
    {
        // Arrange
        const string input = "$sourceDescriptions.petstoreDescription.loginUser";

        // Act
        var result = RuntimeExpressionParser.TryParse(input, out var expression, out var error);

        // Assert
        result.ShouldBeTrue(error ?? string.Empty);
        var sourceDescriptions = expression.ShouldBeOfType<RuntimeExpression.SourceDescriptions>();
        sourceDescriptions.SourceName.ShouldBe("petstoreDescription");
        sourceDescriptions.ReferenceId.ShouldBe("loginUser");
    }

    [Fact]
    public void TryParse_SourceDescriptionsReferenceIdWithDots_ReturnsSourceDescriptionsExpression()
    {
        // Arrange
        // operationIds have no character restrictions per spec §5.9 — must not be dot-split further.
        const string input = "$sourceDescriptions.events.com.example.sendLightMeasurement";

        // Act
        var result = RuntimeExpressionParser.TryParse(input, out var expression, out var error);

        // Assert
        result.ShouldBeTrue(error ?? string.Empty);
        var sourceDescriptions = expression.ShouldBeOfType<RuntimeExpression.SourceDescriptions>();
        sourceDescriptions.SourceName.ShouldBe("events");
        sourceDescriptions.ReferenceId.ShouldBe("com.example.sendLightMeasurement");
    }

    [Fact]
    public void TryParse_ComponentsSuccessAction_ReturnsComponentsExpression()
    {
        // Arrange
        const string input = "$components.successActions.notify";

        // Act
        var result = RuntimeExpressionParser.TryParse(input, out var expression, out var error);

        // Assert
        result.ShouldBeTrue(error ?? string.Empty);
        var components = expression.ShouldBeOfType<RuntimeExpression.Components>();
        components.Field.ShouldBe("successActions");
        components.Name.ShouldBe("notify");
    }

    [Fact]
    public void TryParse_ComponentsParameter_ReturnsComponentsExpression()
    {
        // Arrange
        const string input = "$components.parameters.page";

        // Act
        var result = RuntimeExpressionParser.TryParse(input, out var expression, out var error);

        // Assert
        result.ShouldBeTrue(error ?? string.Empty);
        var components = expression.ShouldBeOfType<RuntimeExpression.Components>();
        components.Field.ShouldBe("parameters");
        components.Name.ShouldBe("page");
    }

    [Fact]
    public void TryParse_ComponentsWithDisallowedField_ReturnsFalse()
    {
        // Arrange
        const string input = "$components.inputs.foo";

        // Act
        var result = RuntimeExpressionParser.TryParse(input, out var expression, out var error);

        // Assert
        result.ShouldBeFalse();
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
        // Arrange

        // Act
        var result = RuntimeExpressionParser.TryParse(input, out var expression, out var error);

        // Assert
        result.ShouldBeFalse();
        expression.ShouldBeNull();
        error.ShouldNotBeNull();
    }
}
