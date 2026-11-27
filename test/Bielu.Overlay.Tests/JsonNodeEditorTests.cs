using System.Text.Json.Nodes;
using Shouldly;
using Xunit;

namespace Bielu.Overlay.Tests;

public class JsonNodeEditorTests
{
    [Fact]
    public void TryRemove_ArrayElement_RemovesItFromItsArray()
    {
        // Arrange
        var root = JsonNode.Parse("""{ "tags": ["a", "b", "c"] }""")!;
        var b = root["tags"]![1]!;

        // Act
        var removed = JsonNodeEditor.TryRemove(b);

        // Assert
        removed.ShouldBeTrue();
        root.ToJsonString().ShouldBe("""{"tags":["a","c"]}""");
    }

    [Fact]
    public void TryRemove_ObjectProperty_RemovesItsKey()
    {
        // Arrange
        var root = JsonNode.Parse("""{ "keep": 1, "drop": { "x": 2 } }""")!;

        // Act
        var removed = JsonNodeEditor.TryRemove(root["drop"]!);

        // Assert
        removed.ShouldBeTrue();
        root.ToJsonString().ShouldBe("""{"keep":1}""");
    }

    [Fact]
    public void TryRemove_Root_ReturnsFalse()
    {
        JsonNodeEditor.TryRemove(JsonNode.Parse("{}")!).ShouldBeFalse();
    }

    [Fact]
    public void TryReplace_ArrayElement_ReplacesItInPlace()
    {
        // Arrange
        var root = JsonNode.Parse("""{ "tags": ["a", "b"] }""")!;

        // Act
        var replaced = JsonNodeEditor.TryReplace(root["tags"]![0]!, JsonValue.Create("z"));

        // Assert
        replaced.ShouldBeTrue();
        root.ToJsonString().ShouldBe("""{"tags":["z","b"]}""");
    }

    [Fact]
    public void TryReplace_ObjectProperty_ReplacesItsValue()
    {
        // Arrange
        var root = JsonNode.Parse("""{ "info": { "title": "Old" } }""")!;

        // Act
        var replaced = JsonNodeEditor.TryReplace(root["info"]!["title"]!, null);

        // Assert
        replaced.ShouldBeTrue();
        root.ToJsonString().ShouldBe("""{"info":{"title":null}}""");
    }

    [Fact]
    public void TryReplace_Root_ReturnsFalse()
    {
        JsonNodeEditor.TryReplace(JsonNode.Parse("[]")!, JsonValue.Create(1)).ShouldBeFalse();
    }
}
