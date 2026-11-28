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
        return StepTargetFingerprint.Of(document["target"], document).Value;
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
        fingerprint.ShouldBe("{\"$ref\":\"#0\",}\n#0={\"properties\":{\"next\":{\"$ref\":\"#0\",},},}");
    }

    [Fact]
    public void Of_WritesASharedDefinitionOnce()
    {
        // Arrange: S0 to S18, each with three properties referencing the next. Inlined, that is 3^18 copies of S18.
        var schemas = new JsonObject();
        for (var i = 0; i < 19; i++)
        {
            var next = new JsonObject { ["$ref"] = $"#/defs/S{i + 1}" };
            schemas[$"S{i}"] = i == 18
                ? new JsonObject { ["type"] = "string" }
                : new JsonObject { ["properties"] = new JsonObject { ["a"] = next.DeepClone(), ["b"] = next.DeepClone(), ["c"] = next } };
        }

        var document = new JsonObject { ["target"] = new JsonObject { ["$ref"] = "#/defs/S0" }, ["defs"] = schemas };

        // Act
        var result = StepTargetFingerprint.Of(document["target"], document);

        // Assert
        result.Complete.ShouldBeTrue();
        result.Value.Length.ShouldBeLessThan(5_000);
        result.Value.ShouldContain("#18={\"type\":\"string\",}");
    }

    [Fact]
    public void Of_IgnoresARenamedComponent()
    {
        Of("""{ "target": { "$ref": "#/defs/a" }, "defs": { "a": { "type": "string" } } }""")
            .ShouldBe(Of("""{ "target": { "$ref": "#/defs/b" }, "defs": { "b": { "type": "string" } } }"""));
    }

    [Fact]
    public void Of_ReferenceChainLongerThanTheDepthLimit_StillSeesItsEnd()
    {
        // Arrange: 70 schemas, each only a $ref to the next, and a last one that differs.
        static string Chain(string last)
        {
            var defs = string.Join(", ", Enumerable.Range(0, 70).Select(i => $"\"s{i}\": {{ \"$ref\": \"#/defs/s{i + 1}\" }}"));
            return $$"""{ "target": { "$ref": "#/defs/s0" }, "defs": { {{defs}}, "s70": { "type": "{{last}}" } } }""";
        }

        // Act & Assert
        Of(Chain("string")).ShouldNotBe(Of(Chain("integer")));
    }

    [Fact]
    public void Of_NestingDeeperThanTheLimit_IsIncomplete()
    {
        // Arrange: 70 objects nested inline, with no $ref to break them up.
        var node = new JsonObject { ["type"] = "string" };
        for (var i = 0; i < 70; i++)
        {
            node = new JsonObject { ["items"] = node };
        }

        var document = new JsonObject { ["target"] = node };

        // Act & Assert
        StepTargetFingerprint.Of(document["target"], document).Complete.ShouldBeFalse();
    }

    [Theory]
    [InlineData("""{ "const": { "description": "A" } }""", """{ "const": { "description": "B" } }""")]
    [InlineData("""{ "enum": [ { "title": "A" } ] }""", """{ "enum": [ { "title": "B" } ] }""")]
    [InlineData("""{ "const": { "$ref": "#/defs/a" } }""", """{ "const": { "$ref": "#/defs/b" } }""")]
    public void Of_KeepsLiteralValuesAsTheyStand(string before, string after)
    {
        // The two $refs resolve to the same schema, but in a const they are data, not references.
        static string With(string target) => $$"""{ "target": {{target}}, "defs": { "a": true, "b": true } }""";
        Of(With(before)).ShouldNotBe(Of(With(after)));
    }

    [Theory]
    [InlineData("""{ "dependentRequired": { "description": ["a"] } }""", """{ "dependentRequired": { "description": ["b"] } }""")]
    [InlineData("""{ "dependencies": { "description": ["a"] } }""", """{ "dependencies": { "description": ["b"] } }""")]
    [InlineData("""{ "discriminator": { "propertyName": "kind", "mapping": { "description": "#/defs/a" } } }""", """{ "discriminator": { "propertyName": "kind", "mapping": { "description": "#/defs/b" } } }""")]
    [InlineData("""{ "default": { "title": "a" } }""", """{ "default": { "title": "b" } }""")]
    [InlineData("""{ "security": [ { "description": ["read"] } ] }""", """{ "security": [ { "description": ["write"] } ] }""")]
    [InlineData("""{ "variables": { "description": { "default": "a" } } }""", """{ "variables": { "description": { "default": "b" } } }""")]
    public void Of_KeepsNamesAndDataNamedLikeDocumentationKeywords(string before, string after)
    {
        static string With(string target) => $$"""{ "target": {{target}}, "defs": { "a": true, "b": true } }""";
        Of(With(before)).ShouldNotBe(Of(With(after)));
    }

    [Theory]
    [InlineData("parameters", """{ "filter": { "description": "A" } }""", """{ "filter": { "description": "B" } }""")]
    [InlineData("requestBody", """{ "title": "A" }""", """{ "title": "B" }""")]
    public void Of_OpenApiLinks_KeepTheirConstants(string field, string before, string after)
    {
        // Arrange: the link inline, and the same link reached through a $ref.
        static string Inline(string field, string value) => $$"""
            { "openapi": "3.1.0", "target": { "responses": { "200": { "links": { "next": { "operationId": "o", "{{field}}": {{value}} } } } } } }
            """;
        static string Referenced(string field, string value) => $$"""
            {
                "openapi": "3.1.0",
                "target": { "responses": { "200": { "links": { "next": { "$ref": "#/components/links/Next" } } } } },
                "components": { "links": { "Next": { "operationId": "o", "description": "d", "{{field}}": {{value}} } } }
            }
            """;

        // Act & Assert
        Of(Inline(field, before)).ShouldNotBe(Of(Inline(field, after)));
        Of(Referenced(field, before)).ShouldNotBe(Of(Referenced(field, after)));
        Of(Referenced(field, before)).ShouldNotContain("\"d\"");
    }

    [Fact]
    public void Of_OpenApiDefaultResponse_IsFollowedLikeAnyResponse()
    {
        // Arrange: default is a status code here, not a schema's default value.
        static string Operation(string type) => $$"""
            {
                "openapi": "3.1.0",
                "target": { "responses": { "default": { "$ref": "#/components/responses/Error" } } },
                "components": { "responses": { "Error": { "description": "e", "content": { "application/json": { "schema": { "type": "{{type}}" } } } } } }
            }
            """;

        // Act & Assert
        Of(Operation("object")).ShouldNotBe(Of(Operation("string")));
        Of(Operation("object")).ShouldNotContain("\"e\"");
    }

    [Fact]
    public void Of_OpenApiResponsesExtension_IsLeftOut()
    {
        // Arrange: an extension on the Responses Object is metadata, not a status code.
        static string Operation(string note) => $$"""
            { "openapi": "3.1.0", "target": { "responses": { "200": { "description": "OK" }, "x-note": "{{note}}" } } }
            """;

        // Act & Assert
        Of(Operation("A")).ShouldBe(Of(Operation("B")));
    }

    [Fact]
    public void Of_AsyncApiSecuritySchemes_AreFollowedAndFiltered()
    {
        // In AsyncAPI 3, security lists Security Scheme Objects or $refs to them: definitions, not requirements.
        static string Operation(string type, string description) => $$"""
            { "target": { "security": [ { "$ref": "#/defs/s" } ] }, "defs": { "s": { "type": "{{type}}", "description": "{{description}}" } } }
            """;

        Of(Operation("httpApiKey", "a")).ShouldNotBe(Of(Operation("userPassword", "a")));
        Of(Operation("httpApiKey", "a")).ShouldBe(Of(Operation("httpApiKey", "b")));
    }

    [Fact]
    public void Of_FollowsReferencesToAnchors()
    {
        // Arrange
        static string Operation(string type) => $$"""
            {
                "openapi": "3.1.0",
                "target": { "schema": { "$ref": "#Item" } },
                "components": { "schemas": { "Item": { "$anchor": "Item", "type": "{{type}}" } } }
            }
            """;

        // Act & Assert
        Of(Operation("string")).ShouldNotBe(Of(Operation("integer")));
    }

    [Theory]
    [InlineData("""{ "target": { "schema": { "$ref": "#Missing" } } }""")]
    [InlineData("""{ "target": { "schema": { "$ref": "#Item" } }, "a": { "$anchor": "Item" }, "b": { "$anchor": "Item" } }""")]
    public void Of_AnchorThatCannotBeFollowed_IsIncomplete(string json)
    {
        // Arrange
        var document = JsonNode.Parse(json)!;

        // Act & Assert
        StepTargetFingerprint.Of(document["target"], document).Complete.ShouldBeFalse();
    }

    [Fact]
    public void Of_FollowsReferencesToBooleanSchemas()
    {
        Of("""{ "target": { "schema": { "$ref": "#/defs/allowed" } }, "defs": { "allowed": true } }""")
            .ShouldNotBe(Of("""{ "target": { "schema": { "$ref": "#/defs/allowed" } }, "defs": { "allowed": false } }"""));
    }

    [Fact]
    public void Of_OpenApiResponseHeaders_KeepsHeaderNamesLikeExtensions()
    {
        static string Response(string type) => $$"""
            {
                "openapi": "3.1.0",
                "target": { "responses": { "200": { "headers": { "x-request-id": { "description": "d", "schema": { "type": "{{type}}" } } } } } }
            }
            """;

        Of(Response("string")).ShouldNotBe(Of(Response("integer")));
        Of(Response("string")).ShouldNotContain("\"description\"");
    }

    [Fact]
    public void Of_AsyncApiMessageHeaders_AreASchema()
    {
        // In AsyncAPI, headers is a schema: its description is documentation, not a header called "description".
        Of("""{ "asyncapi": "3.0.0", "target": { "headers": { "type": "object", "description": "a" } } }""")
            .ShouldBe(Of("""{ "asyncapi": "3.0.0", "target": { "headers": { "type": "object", "description": "b" } } }"""));
    }

    [Fact]
    public void Of_IncludesKeywordsBesideAReference()
    {
        Of("""{ "target": { "$ref": "#/defs/a", "maxLength": 20 }, "defs": { "a": { "type": "string" } } }""")
            .ShouldNotBe(Of("""{ "target": { "$ref": "#/defs/a", "maxLength": 5 }, "defs": { "a": { "type": "string" } } }"""));
    }

    [Fact]
    public void Of_KeepsAsyncApiMessageIdsNamedLikeDocumentationKeywords()
    {
        Of("""{ "target": { "messages": { "description": { "$ref": "#/m/a" } } }, "m": { "a": { "payload": { "type": "string" } } } }""")
            .ShouldNotBe(Of("""{ "target": { "messages": { "description": { "$ref": "#/m/a" } } }, "m": { "a": { "payload": { "type": "integer" } } } }"""));
    }

    [Fact]
    public void Of_OpenApiOperation_IncludesThePathItemParametersItDoesNotOverride()
    {
        // Arrange
        static string Operation(string idType, string pathLimitType) => $$"""
            {
                "openapi": "3.1.0",
                "paths": {
                    "/orders/{id}": {
                        "parameters": [
                            { "name": "id", "in": "path", "schema": { "type": "{{idType}}" } },
                            { "name": "limit", "in": "query", "schema": { "type": "{{pathLimitType}}" } }
                        ],
                        "get": { "parameters": [ { "name": "limit", "in": "query", "schema": { "type": "integer" } } ] },
                        "additionalOperations": { "COPY": { } }
                    }
                }
            }
            """;
        static StepTargetFingerprint.Result Get(string json, string method)
        {
            var document = JsonNode.Parse(json)!;
            var pathItem = document["paths"]!["/orders/{id}"]!;
            return StepTargetFingerprint.Of(method == "COPY" ? pathItem["additionalOperations"]!["COPY"] : pathItem[method], document);
        }

        // Act & Assert: a path-level parameter counts, also for an additional operation; one the operation
        // overrides, as get does limit, doesn't.
        Get(Operation("string", "integer"), "get").Value.ShouldNotBe(Get(Operation("integer", "integer"), "get").Value);
        Get(Operation("string", "integer"), "get").Value.ShouldBe(Get(Operation("string", "boolean"), "get").Value);
        Get(Operation("string", "integer"), "COPY").Value.ShouldNotBe(Get(Operation("integer", "integer"), "COPY").Value);
        Get(Operation("string", "integer"), "COPY").Value.ShouldNotBe(Get(Operation("string", "boolean"), "COPY").Value);
    }

    [Fact]
    public void Of_LeavesUnresolvableAndExternalReferencesAsWritten()
    {
        Of("""{ "target": { "a": { "$ref": "#/missing" }, "b": { "$ref": "other.json#/x" } } }""")
            .ShouldBe("{\"a\":{\"$ref\":\"#/missing\",},\"b\":{\"$ref\":\"other.json#/x\",},}");
    }

    [Fact]
    public void Of_DistinguishesArrayOrderAndScalars()
    {
        Of("""{ "target": [1, 2] }""").ShouldNotBe(Of("""{ "target": [2, 1] }"""));
        Of("""{ "target": { "a": null } }""").ShouldBe("{\"a\":null,}");
    }
}
