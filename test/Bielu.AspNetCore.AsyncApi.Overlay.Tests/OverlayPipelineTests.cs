using Bielu.AspNetCore.Overlay;
using Bielu.Overlay.Readers;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Overlay.Tests;

/// <summary>Unit tests for the format-aware pipeline shared by the AsyncAPI and Arazzo integrations.</summary>
public class OverlayPipelineTests
{
    private const string RemoveChannelOverlay = """
        overlay: 1.1.0
        info:
          title: Strip internal
          version: 1.0.0
        actions:
          - target: $.channels.internal
            remove: true
        """;

    [Fact]
    public void Apply_WithNoSources_ReturnsInputUnchanged()
    {
        // Arrange
        var pipeline = new OverlayPipeline();
        const string json = """{"asyncapi":"3.0.0"}""";

        // Act
        var result = pipeline.Apply(json, OverlayDocumentFormat.Json, "v1");

        // Assert
        pipeline.IsEmpty.ShouldBeTrue();
        result.ShouldBeSameAs(json);
    }

    [Fact]
    public void Apply_RemoveAction_StripsTheTargetedNode()
    {
        // Arrange
        var pipeline = new OverlayPipeline();
        pipeline.Add(OverlaySource.FromDocument(OverlayStringReader.Read(RemoveChannelOverlay).Document!));
        const string json = """{"asyncapi":"3.0.0","channels":{"public":{},"internal":{}}}""";

        // Act
        var result = pipeline.Apply(json, OverlayDocumentFormat.Json, "v1");

        // Assert
        result.ShouldContain("public");
        result.ShouldNotContain("internal");
    }

    [Fact]
    public void Apply_YamlInput_RoundTripsThroughYaml()
    {
        // Arrange
        var pipeline = new OverlayPipeline();
        pipeline.Add(OverlaySource.FromDocument(OverlayStringReader.Read(RemoveChannelOverlay).Document!));
        const string yaml = """
            asyncapi: 3.0.0
            channels:
              public: {}
              internal: {}
            """;

        // Act
        var result = pipeline.Apply(yaml, OverlayDocumentFormat.Yaml, "v1");

        // Assert
        result.ShouldContain("public");
        result.ShouldNotContain("internal");
        result.TrimStart().ShouldNotStartWith("{");
    }

    [Fact]
    public void Apply_UnparseableDocument_ThrowsWithTheDocumentName()
    {
        // Arrange
        var pipeline = new OverlayPipeline();
        pipeline.Add(OverlaySource.FromDocument(OverlayStringReader.Read(RemoveChannelOverlay).Document!));

        // Act
        var exception = Should.Throw<OverlayApplicationException>(
            () => pipeline.Apply("{not json", OverlayDocumentFormat.Json, "v1"));

        // Assert
        exception.Message.ShouldContain("v1");
    }

    [Fact]
    public void FromFile_MissingFile_ThrowsOnResolveRatherThanOnRegistration()
    {
        // Arrange — deferred resolution is what keeps a missing overlay from breaking service registration.
        var source = OverlaySource.FromFile(Path.Combine(Path.GetTempPath(), $"absent-{Guid.NewGuid():N}.yaml"));

        // Act
        var exception = Should.Throw<OverlayApplicationException>(() => source.Resolve());

        // Assert
        exception.Message.ShouldContain("Failed to read overlay");
    }

    [Fact]
    public void FromFile_InvalidOverlay_ThrowsWithDiagnostics()
    {
        // Arrange
        var path = TestOverlays.WriteTempOverlay("overlay: 1.1.0\ninfo:\n  title: No actions\n  version: 1.0.0\n");
        try
        {
            var source = OverlaySource.FromFile(path);

            // Act
            var exception = Should.Throw<OverlayApplicationException>(() => source.Resolve());

            // Assert
            exception.Message.ShouldContain("is not valid");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void FromFile_ReadsTheFileOnlyOnce()
    {
        // Arrange
        var path = TestOverlays.WriteTempOverlay(RemoveChannelOverlay);
        var source = OverlaySource.FromFile(path);
        var first = source.Resolve();

        // Act — deleting the file must not affect a source that has already resolved it.
        File.Delete(path);
        var second = source.Resolve();

        // Assert
        second.ShouldBeSameAs(first);
    }
}
