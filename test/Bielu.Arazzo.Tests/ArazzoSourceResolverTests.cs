using System.Text.Json.Nodes;
using Shouldly;
using Xunit;

namespace Bielu.Arazzo.Tests;

public class ArazzoSourceResolverTests
{
    private sealed class FakeDocument
    {
        public Dictionary<string, JsonNode> Operations { get; } = new(StringComparer.Ordinal);
    }

    private sealed class FakeResolver : ArazzoSourceResolver<FakeDocument>
    {
        public override string SourceType => "fake";

        public override bool TryResolveOperation(FakeDocument document, string operationId, out JsonNode? operation)
        {
            var found = document.Operations.TryGetValue(operationId, out var node);
            operation = node;
            return found;
        }
    }

    private static ArazzoWorkspace WorkspaceWith(object document)
    {
        var workspace = new ArazzoWorkspace();
        workspace.RegisterResolver(new FakeResolver());
        workspace.RegisterDocument("source", "fake", document);
        return workspace;
    }

    [Fact]
    public void DocumentType_IsTheTypeArgument()
    {
        new FakeResolver().DocumentType.ShouldBe(typeof(FakeDocument));
    }

    [Fact]
    public void TryResolveOperation_ThroughTheWorkspace_PassesTheTypedDocument()
    {
        // Arrange
        var document = new FakeDocument();
        document.Operations["getOrder"] = new JsonObject { ["summary"] = "Get an order" };
        var workspace = WorkspaceWith(document);

        // Act
        var resolved = workspace.TryResolveOperation("source", "getOrder", out var operation);

        // Assert
        resolved.ShouldBeTrue();
        operation!["summary"]!.GetValue<string>().ShouldBe("Get an order");
        workspace.TryResolveOperation("source", "missing", out _).ShouldBeFalse();
    }

    [Fact]
    public void PathMethods_NotOverridden_DoNotResolve()
    {
        // Arrange
        var workspace = WorkspaceWith(new FakeDocument());

        // Act & Assert
        workspace.TryResolveOperationPath("source", "/paths/~1orders/get", out var operation).ShouldBeFalse();
        operation.ShouldBeNull();
        workspace.TryResolveChannelPath("source", "/channels/orders", out var channel).ShouldBeFalse();
        channel.ShouldBeNull();
    }

    [Fact]
    public void Resolve_DocumentOfTheWrongType_ThrowsArgumentException()
    {
        // Arrange
        var workspace = WorkspaceWith("not a fake document");

        // Act
        var exception = Should.Throw<ArgumentException>(() => workspace.TryResolveOperation("source", "getOrder", out _));

        // Assert
        exception.Message.ShouldContain("FakeDocument");
        exception.Message.ShouldContain("String");
    }

    [Fact]
    public void UntypedMethods_RejectNullArguments()
    {
        // Arrange
        IArazzoSourceResolver resolver = new FakeResolver();
        var document = new FakeDocument();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => resolver.TryResolveOperation(null!, "id", out _));
        Should.Throw<ArgumentNullException>(() => resolver.TryResolveOperation(document, null!, out _));
        Should.Throw<ArgumentNullException>(() => resolver.TryResolveOperationPath(document, null!, out _));
        Should.Throw<ArgumentNullException>(() => resolver.TryResolveChannelPath(document, null!, out _));
    }
}
