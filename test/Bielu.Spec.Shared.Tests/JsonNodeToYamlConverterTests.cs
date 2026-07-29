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
        var result = RoundTrip("""
        {"text":"hello","integer":42,"negative":-7,"floating":1.5,"yes":true,"no":false,"nothing":null}
        """);

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
        // Without quoting these come back as a bool, a number and a null respectively.
        var result = RoundTrip("""{"a":"true","b":"42","c":"null","d":"1.5"}""");

        result["a"]!.GetValue<string>().ShouldBe("true");
        result["b"]!.GetValue<string>().ShouldBe("42");
        result["c"]!.GetValue<string>().ShouldBe("null");
        result["d"]!.GetValue<string>().ShouldBe("1.5");
    }

    [Fact]
    public void RoundTrip_QuotesValuesThatWouldParseAsYamlStructure()
    {
        // Arazzo's channelPath is exactly this shape; unquoted, the leading '{' starts a flow mapping.
        var result = RoundTrip("""
        {"channelPath":"{$sourceDescriptions.events.url}#/channels/lightingAlert","seq":"[a, b]"}
        """);

        result["channelPath"]!.GetValue<string>()
            .ShouldBe("{$sourceDescriptions.events.url}#/channels/lightingAlert");
        result["seq"]!.GetValue<string>().ShouldBe("[a, b]");
    }

    [Fact]
    public void RoundTrip_PreservesNestedObjectsAndArrays()
    {
        var result = RoundTrip("""
        {"workflows":[{"workflowId":"w","steps":[{"stepId":"a"},{"stepId":"b"}]}],"empty":{},"none":[]}
        """);

        var steps = result["workflows"]![0]!["steps"]!.AsArray();
        steps.Count.ShouldBe(2);
        steps[1]!["stepId"]!.GetValue<string>().ShouldBe("b");
        result["empty"]!.AsObject().Count.ShouldBe(0);
        result["none"]!.AsArray().Count.ShouldBe(0);
    }

    [Fact]
    public void RoundTrip_PreservesKeysThatNeedQuoting()
    {
        // OpenAPI path keys start with '/', and response codes are numeric-looking keys.
        var result = RoundTrip("""{"paths":{"/items":{"get":{"200":{"description":"OK"}}}}}""");

        result["paths"]!["/items"]!["get"]!["200"]!["description"]!.GetValue<string>().ShouldBe("OK");
    }

    [Fact]
    public void RoundTrip_DistinguishesNullFromTheEmptyString()
    {
        // YAML writes null as an empty *plain* scalar (`description:`) and an empty string as a quoted
        // one (`empty: ""`). Conflating the two turns every absent value into "".
        var result = RoundTrip("""{"nothing":null,"empty":""}""");

        result["nothing"].ShouldBeNull();
        result["empty"]!.GetValue<string>().ShouldBe("");
    }

    [Fact]
    public void Serialize_Null_ProducesAnEmptyDocument()
    {
        JsonNodeToYamlConverter.Serialize(null).Trim().ShouldBeOneOf("", "---", "null");
    }
}
