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
        var value = ArazzoValue.FromLiteral(null);

        value.Kind.ShouldBe(ArazzoValueKind.Literal);
        value.IsLiteral.ShouldBeTrue();
        value.IsExpression.ShouldBeFalse();
        value.IsSelector.ShouldBeFalse();
        value.Literal.ShouldBeNull();
    }

    [Fact]
    public void FromExpression_ValidExpression_SetsExpressionKindOnly()
    {
        var value = ArazzoValue.FromExpression("$inputs.username");

        value.Kind.ShouldBe(ArazzoValueKind.Expression);
        value.Expression.ShouldBe("$inputs.username");
        value.Literal.ShouldBeNull();
        value.Selector.ShouldBeNull();
    }

    [Fact]
    public void FromSelector_ValidSelector_SetsSelectorKindOnly()
    {
        var selector = new ArazzoSelector { Context = "$response.body", Selector = "$.id", Type = ArazzoSelectorType.Simple };

        var value = ArazzoValue.FromSelector(selector);

        value.Kind.ShouldBe(ArazzoValueKind.Selector);
        value.Selector.ShouldBe(selector);
        value.Literal.ShouldBeNull();
        value.Expression.ShouldBeNull();
    }

    [Fact]
    public void ImplicitStringConversion_ExpressionString_ProducesExpressionVariant()
    {
        ArazzoValue value = "$steps.a.outputs.b";

        value.IsExpression.ShouldBeTrue();
        value.Expression.ShouldBe("$steps.a.outputs.b");
    }
}

public class ArazzoSelectorTypeTests
{
    [Fact]
    public void Simple_AccessedTwice_ReturnsIndependentInstances()
    {
        var first = ArazzoSelectorType.Simple;
        var second = ArazzoSelectorType.Simple;

        first.ShouldNotBeSameAs(second);

        first.Type = "mutated";

        second.Type.ShouldBe("simple");
    }
}

public class ArazzoReferenceableTests
{
    [Fact]
    public void SerializeAsV1_NeitherValueNorReferenceSet_ThrowsInvalidOperationException()
    {
        var referenceable = new ArazzoReferenceable<ArazzoSuccessAction>();

        Should.Throw<InvalidOperationException>(() => referenceable.SerializeAsV1(new ArazzoJsonNodeWriter()));
    }

    [Fact]
    public void SerializeAsV1_ReferenceSet_WritesReferenceWithoutThrowing()
    {
        var referenceable = ArazzoReferenceable<ArazzoSuccessAction>.Of(new ArazzoReusableObject { Reference = "$components.successActions.notify" });

        Should.NotThrow(() => referenceable.SerializeAsV1(new ArazzoJsonNodeWriter()));
    }
}

public class ArazzoExtensibleWriterExtensionsTests
{
    [Fact]
    public void WriteExtensions_NonExtensionKey_ThrowsArgumentException()
    {
        var writer = new ArazzoJsonNodeWriter();
        var extensions = new Dictionary<string, JsonNode?> { ["title"] = JsonValue.Create("bad") };

        writer.WriteStartObject();
        Should.Throw<ArgumentException>(() => writer.WriteExtensions(extensions));
    }

    [Fact]
    public void WriteExtensions_ExtensionKey_WritesRawValue()
    {
        var writer = new ArazzoJsonNodeWriter();
        var extensions = new Dictionary<string, JsonNode?> { ["x-note"] = JsonValue.Create("ok") };

        writer.WriteStartObject();
        writer.WriteExtensions(extensions);
        writer.WriteEndObject();

        writer.Result!["x-note"]!.GetValue<string>().ShouldBe("ok");
    }
}
