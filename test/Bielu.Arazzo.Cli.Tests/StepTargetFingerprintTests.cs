// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Nodes;
using Bielu.Arazzo.Cli.Commands;
using Shouldly;
using Xunit;

namespace Bielu.Arazzo.Cli.Tests;

public class StepTargetFingerprintTests
{
    private static string Of(string json)
    {
        var document = JsonNode.Parse(json)!;
        return StepTargetFingerprint.Of(document["target"], document);
    }

    [Fact]
    public void Of_IgnoresPropertyOrder()
    {
        Of("""{ "target": { "a": 1, "b": 2 } }""").ShouldBe(Of("""{ "target": { "b": 2, "a": 1 } }"""));
    }

    [Fact]
    public void Of_IgnoresDocumentationKeywordsAndExtensions()
    {
        Of("""{ "target": { "type": "string" } }""")
            .ShouldBe(Of("""{ "target": { "type": "string", "description": "d", "summary": "s", "x-internal": true, "examples": [1] } }"""));
    }

    [Fact]
    public void Of_KeepsSchemaPropertiesNamedLikeDocumentationKeywords()
    {
        Of("""{ "target": { "properties": { "description": { "type": "string" } } } }""")
            .ShouldNotBe(Of("""{ "target": { "properties": { } } }"""));
    }

    [Fact]
    public void Of_FollowsLocalReferences()
    {
        Of("""{ "target": { "$ref": "#/defs/a" }, "defs": { "a": { "type": "string" } } }""")
            .ShouldNotBe(Of("""{ "target": { "$ref": "#/defs/a" }, "defs": { "a": { "type": "integer" } } }"""));
    }

    [Fact]
    public void Of_StopsAtCyclicReferences()
    {
        var fingerprint = Of("""{ "target": { "$ref": "#/defs/node" }, "defs": { "node": { "properties": { "next": { "$ref": "#/defs/node" } } } } }""");
        fingerprint.ShouldContain("\"$ref\":\"#/defs/node\"");
    }

    [Fact]
    public void Of_LeavesUnresolvableAndExternalReferencesAsWritten()
    {
        Of("""{ "target": { "a": { "$ref": "#/missing" }, "b": { "$ref": "other.json#/x" } } }""")
            .ShouldBe("{\"a\":{\"$ref\":\"#/missing\"},\"b\":{\"$ref\":\"other.json#/x\"},}");
    }

    [Fact]
    public void Of_DistinguishesArrayOrderAndScalars()
    {
        Of("""{ "target": [1, 2] }""").ShouldNotBe(Of("""{ "target": [2, 1] }"""));
        Of("""{ "target": { "a": null } }""").ShouldBe("{\"a\":null,}");
    }
}
