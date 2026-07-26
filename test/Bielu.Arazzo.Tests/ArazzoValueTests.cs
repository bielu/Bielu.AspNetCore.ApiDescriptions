using System.Text.Json.Nodes;
using Bielu.Arazzo.Models;
using Bielu.Arazzo.Writers;
using Shouldly;
using Xunit;

namespace Bielu.Arazzo.Tests;

public class ArazzoValueTests
{
    [Fact]
    public void FromLiteral_NullLiteral_PreservesLiteralKindRatherThanUnset()
    {
        // Arrange

        // Act
        var value = ArazzoValue.FromLiteral(null);

        // Assert
        value.Kind.ShouldBe(ArazzoValueKind.Literal);
        value.IsLiteral.ShouldBeTrue();
        value.IsExpression.ShouldBeFalse();
        value.IsSelector.ShouldBeFalse();
        value.Literal.ShouldBeNull();
    }

    [Fact]
    public void FromExpression_ValidExpression_SetsExpressionKindOnly()
    {
        // Arrange
        const string expression = "$inputs.username";

        // Act
        var value = ArazzoValue.FromExpression(expression);

        // Assert
        value.Kind.ShouldBe(ArazzoValueKind.Expression);
        value.Expression.ShouldBe(expression);
        value.Literal.ShouldBeNull();
        value.Selector.ShouldBeNull();
    }

    [Fact]
    public void FromSelector_ValidSelector_SetsSelectorKindOnly()
    {
        // Arrange
        var selector = new ArazzoSelector { Context = "$response.body", Selector = "$.id", Type = ArazzoSelectorType.Simple };

        // Act
        var value = ArazzoValue.FromSelector(selector);

        // Assert
        value.Kind.ShouldBe(ArazzoValueKind.Selector);
        value.Selector.ShouldBe(selector);
        value.Literal.ShouldBeNull();
        value.Expression.ShouldBeNull();
    }

    [Fact]
    public void ImplicitStringConversion_ExpressionString_ProducesExpressionVariant()
    {
        // Arrange
        const string expression = "$steps.a.outputs.b";

        // Act
        ArazzoValue value = expression;

        // Assert
        value.IsExpression.ShouldBeTrue();
        value.Expression.ShouldBe(expression);
    }
}

public class ArazzoSelectorTypeTests
{
    [Fact]
    public void Simple_AccessedTwice_ReturnsIndependentInstances()
    {
        // Arrange

        // Act
        var first = ArazzoSelectorType.Simple;
        var second = ArazzoSelectorType.Simple;
        first.Type = "mutated";

        // Assert
        first.ShouldNotBeSameAs(second);
        second.Type.ShouldBe("simple");
    }
}

public class ArazzoReferenceableTests
{
    [Fact]
    public void SerializeAsV1_NeitherValueNorReferenceSet_ThrowsInvalidOperationException()
    {
        // Arrange
        var referenceable = new ArazzoReferenceable<ArazzoSuccessAction>();
        var writer = new ArazzoJsonNodeWriter();

        // Act
        var action = () => referenceable.SerializeAsV1(writer);

        // Assert
        Should.Throw<InvalidOperationException>(action);
    }

    [Fact]
    public void SerializeAsV1_ReferenceSet_WritesReferenceWithoutThrowing()
    {
        // Arrange
        var referenceable = ArazzoReferenceable<ArazzoSuccessAction>.Of(new ArazzoReusableObject { Reference = "$components.successActions.notify" });
        var writer = new ArazzoJsonNodeWriter();

        // Act
        var action = () => referenceable.SerializeAsV1(writer);

        // Assert
        Should.NotThrow(action);
    }
}

public class ArazzoFailureActionTests
{
    [Fact]
    public void SerializeAsV1_NullWriter_ThrowsArgumentNullException()
    {
        // Arrange
        var failureAction = new ArazzoFailureAction { Name = "stop", Type = ArazzoFailureActionType.End };

        // Act
        var exception = Record.Exception(() => failureAction.SerializeAsV1(null!));

        // Assert
        exception.ShouldBeOfType<ArgumentNullException>();
    }
}

public class ArazzoExtensibleWriterExtensionsTests
{
    [Fact]
    public void WriteExtensions_NonExtensionKey_ThrowsArgumentException()
    {
        // Arrange
        var writer = new ArazzoJsonNodeWriter();
        var extensions = new Dictionary<string, JsonNode?> { ["title"] = JsonValue.Create("bad") };
        writer.WriteStartObject();
        var action = () => writer.WriteExtensions(extensions);

        // Act
        var exception = Record.Exception(action);

        // Assert
        exception.ShouldBeOfType<ArgumentException>();
    }

    [Fact]
    public void WriteExtensions_ExtensionKey_WritesRawValue()
    {
        // Arrange
        var writer = new ArazzoJsonNodeWriter();
        var extensions = new Dictionary<string, JsonNode?> { ["x-note"] = JsonValue.Create("ok") };

        // Act
        writer.WriteStartObject();
        writer.WriteExtensions(extensions);
        writer.WriteEndObject();

        // Assert
        writer.Result!["x-note"]!.GetValue<string>().ShouldBe("ok");
    }
}
