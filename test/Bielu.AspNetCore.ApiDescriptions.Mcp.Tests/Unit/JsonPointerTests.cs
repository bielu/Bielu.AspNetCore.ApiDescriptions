using System.Text.Json.Nodes;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.ApiDescriptions.Mcp.Tests.Unit;

public class JsonPointerTests
{
    [Fact]
    public void Build_EscapesTildeAndSlash()
    {
        // Act & Assert
        JsonPointer.Build("channels", "orders/v1~beta").ShouldBe("#/channels/orders~1v1~0beta");
        JsonPointer.Append("#/a", "b/c", "0").ShouldBe("#/a/b~1c/0");
        JsonPointer.Build().ShouldBe("#");
    }

    [Theory]
    [InlineData("#/channels/orders~1v1~0beta", new[] { "channels", "orders/v1~beta" })]
    [InlineData("#/channels/orders%7E1v1%7E0beta", new[] { "channels", "orders/v1~beta" })]
    [InlineData("#/a%20b/~01", new[] { "a b", "~1" })]
    [InlineData("#", new string[0])]
    public void TryParse_DecodesPercentEncodingThenTildeEscapes(string reference, string[] expected)
    {
        // Act
        var parsed = JsonPointer.TryParse(reference, out var segments);

        // Assert
        parsed.ShouldBeTrue();
        segments.ShouldBe(expected);
    }

    [Theory]
    [InlineData("other.json#/a")]
    [InlineData("#plain-name")]
    public void TryParse_NotALocalPointer_ReturnsFalse(string reference)
    {
        // Act & Assert
        JsonPointer.TryParse(reference, out _).ShouldBeFalse();
    }

    [Fact]
    public void Normalize_WritesTheCanonicalForm()
    {
        // Act & Assert
        JsonPointer.Normalize("#/channels/orders%7E1v1").ShouldBe("#/channels/orders~1v1");
        JsonPointer.Normalize("https://example.com/x.json").ShouldBe("https://example.com/x.json");
    }

    [Fact]
    public void Resolve_FollowsReferencesOnTheWayAndIndexesArrays()
    {
        // Arrange
        var document = JsonNode.Parse("""
                                      {
                                        "channels": { "orders": { "$ref": "#/components/channels/orders" } },
                                        "components": { "channels": { "orders": { "messages": { "created": { "name": "Created" } }, "tags": [ "a", "b" ] } } }
                                      }
                                      """)!;

        // Act & Assert
        JsonPointer.Resolve(document, "#/channels/orders/messages/created")!["name"]!.GetValue<string>()
            .ShouldBe("Created");
        JsonPointer.Resolve(document, "#/components/channels/orders/tags/1")!.GetValue<string>().ShouldBe("b");
        JsonPointer.Resolve(document, "#/components/channels/orders/tags/9").ShouldBeNull();
        JsonPointer.Resolve(document, "#/nothing/here").ShouldBeNull();
        JsonPointer.Resolve(document, "#").ShouldBeSameAs(document);
    }

    [Fact]
    public void Follow_ReturnsTheEndOfTheReferenceChain()
    {
        // Arrange
        var document = JsonNode.Parse("""
                                      {
                                        "a": { "$ref": "#/b" },
                                        "b": { "$ref": "#/c" },
                                        "c": { "type": "string" },
                                        "loop1": { "$ref": "#/loop2" },
                                        "loop2": { "$ref": "#/loop1" },
                                        "away": { "$ref": "other.json#/x" },
                                        "scalar": 1
                                      }
                                      """)!;

        // Act & Assert
        JsonPointer.Follow(document, "#/a").ShouldBe(("#/c", document["c"]!.AsObject(), null));
        JsonPointer.Follow(document, "#/loop1").Problem!.ShouldContain("cycle");
        JsonPointer.Follow(document, "#/away").Problem!.ShouldContain("another document");
        JsonPointer.Follow(document, "#/missing").Problem!.ShouldContain("does not exist");
        JsonPointer.Follow(document, "#/scalar").Problem!.ShouldContain("not an object");
    }

    [Fact]
    public void ReferenceOf_ReadsOnlyNonEmptyStringReferences()
    {
        // Act & Assert
        JsonPointer.ReferenceOf(JsonNode.Parse("""{ "$ref": "#/a" }""")).ShouldBe("#/a");
        JsonPointer.ReferenceOf(JsonNode.Parse("""{ "$ref": 1 }""")).ShouldBeNull();
        JsonPointer.ReferenceOf(JsonNode.Parse("""{ "$ref": "" }""")).ShouldBeNull();
        JsonPointer.ReferenceOf(JsonNode.Parse("[]")).ShouldBeNull();
        JsonPointer.ReferenceOf(null).ShouldBeNull();
    }
}
