// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Nodes;
using Bielu.Spec.Shared;
using Shouldly;
using Xunit;

namespace Bielu.Spec.Shared.Tests;

public class JsonNodeToYamlConverterTests
{
    /// <summary>Serializes to YAML and reads it straight back, which is the property that actually matters.</summary>
    private static JsonNode RoundTrip(string json)
    {
        var yaml = JsonNodeToYamlConverter.Serialize(JsonNode.Parse(json));
        return YamlToJsonNodeConverter.Convert(new StringReader(yaml))
               ?? throw new InvalidOperationException("YAML round-trip produced no document.");
    }

    [Fact]
    public void RoundTrip_PreservesScalarTypes()
    {
        // Arrange
        const string json = """
        {"text":"hello","integer":42,"negative":-7,"floating":1.5,"yes":true,"no":false,"nothing":null}
        """;

        // Act
        var result = RoundTrip(json);

        // Assert
        result["text"]!.GetValue<string>().ShouldBe("hello");
        result["integer"]!.GetValue<int>().ShouldBe(42);
        result["negative"]!.GetValue<int>().ShouldBe(-7);
        result["floating"]!.GetValue<double>().ShouldBe(1.5);
        result["yes"]!.GetValue<bool>().ShouldBeTrue();
        result["no"]!.GetValue<bool>().ShouldBeFalse();
        result["nothing"].ShouldBeNull();
    }

    [Fact]
    public void RoundTrip_QuotesStringsThatWouldOtherwiseChangeType()
    {
        // Arrange
        // Without quoting these come back as a bool, a number and a null respectively.
        const string json = """{"a":"true","b":"42","c":"null","d":"1.5"}""";

        // Act
        var result = RoundTrip(json);

        // Assert
        result["a"]!.GetValue<string>().ShouldBe("true");
        result["b"]!.GetValue<string>().ShouldBe("42");
        result["c"]!.GetValue<string>().ShouldBe("null");
        result["d"]!.GetValue<string>().ShouldBe("1.5");
    }

    [Fact]
    public void RoundTrip_QuotesValuesThatWouldParseAsYamlStructure()
    {
        // Arrange
        // Arazzo's channelPath is exactly this shape; unquoted, the leading '{' starts a flow mapping.
        const string json = """
        {"channelPath":"{$sourceDescriptions.events.url}#/channels/lightingAlert","seq":"[a, b]"}
        """;

        // Act
        var result = RoundTrip(json);

        // Assert
        result["channelPath"]!.GetValue<string>()
            .ShouldBe("{$sourceDescriptions.events.url}#/channels/lightingAlert");
        result["seq"]!.GetValue<string>().ShouldBe("[a, b]");
    }

    [Fact]
    public void RoundTrip_PreservesNestedObjectsAndArrays()
    {
        // Arrange
        const string json = """
        {"workflows":[{"workflowId":"w","steps":[{"stepId":"a"},{"stepId":"b"}]}],"empty":{},"none":[]}
        """;

        // Act
        var result = RoundTrip(json);

        // Assert
        var steps = result["workflows"]![0]!["steps"]!.AsArray();
        steps.Count.ShouldBe(2);
        steps[1]!["stepId"]!.GetValue<string>().ShouldBe("b");
        result["empty"]!.AsObject().Count.ShouldBe(0);
        result["none"]!.AsArray().Count.ShouldBe(0);
    }

    [Fact]
    public void RoundTrip_PreservesKeysThatNeedQuoting()
    {
        // Arrange
        // OpenAPI path keys start with '/', and response codes are numeric-looking keys.
        const string json = """{"paths":{"/items":{"get":{"200":{"description":"OK"}}}}}""";

        // Act
        var result = RoundTrip(json);

        // Assert
        result["paths"]!["/items"]!["get"]!["200"]!["description"]!.GetValue<string>().ShouldBe("OK");
    }

    [Fact]
    public void RoundTrip_DistinguishesNullFromTheEmptyString()
    {
        // Arrange
        // YAML writes null as an empty *plain* scalar (`description:`) and an empty string as a quoted
        // one (`empty: ""`). Conflating the two turns every absent value into "".
        const string json = """{"nothing":null,"empty":""}""";

        // Act
        var result = RoundTrip(json);

        // Assert
        result["nothing"].ShouldBeNull();
        result["empty"]!.GetValue<string>().ShouldBe("");
    }

    [Fact]
    public void Serialize_Null_ProducesAnEmptyDocument()
    {
        // Arrange & Act
        var yaml = JsonNodeToYamlConverter.Serialize(null);

        // Assert
        yaml.Trim().ShouldBeOneOf("", "---", "null");
    }
}
