using Scalar.AspNetCore;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Scalar.Broker.Tests;

/// <summary>
/// <c>WithBrokerClient</c>'s effect on the Scalar page: the script tag, and the optional explicit
/// document override.
/// </summary>
public class ScalarBrokerOptionsExtensionsTests
{
    [Fact]
    public void WithBrokerClient_AddsThePluginScript()
    {
        // Arrange
        var options = new ScalarOptions();

        // Act
        options.WithBrokerClient();

        // Assert
        options.HeadContent.ShouldNotBeNull().ShouldContain("<script src=\"/bielu/scalar/broker/plugin.js\"></script>");
    }

    [Fact]
    public void WithBrokerClient_WithoutConfigure_EmitsNoDocumentOverride()
    {
        // Arrange — with no override the bundle auto-discovers documents from Scalar's own sources,
        // so emitting an empty global would needlessly suppress that.
        var options = new ScalarOptions();

        // Act
        options.WithBrokerClient();

        // Assert
        options.HeadContent.ShouldNotBeNull().ShouldNotContain("__BIELU_SCALAR_BROKER__");
    }

    [Fact]
    public void WithBrokerClient_WithDocuments_EmitsTheOverrideBeforeTheScript()
    {
        // Arrange
        var options = new ScalarOptions();

        // Act
        options.WithBrokerClient(broker => broker.AddDocument("v1", "/asyncapi/v1.json"));

        // Assert
        var content = options.HeadContent!;
        content.ShouldContain("__BIELU_SCALAR_BROKER__");
        content.ShouldContain("/asyncapi/v1.json");
        // The config must be assigned before the bundle runs, or the bundle will not see it.
        content.IndexOf("__BIELU_SCALAR_BROKER__", StringComparison.Ordinal)
            .ShouldBeLessThan(content.IndexOf("plugin.js", StringComparison.Ordinal));
    }

    [Fact]
    public void WithBrokerClient_CustomAssetsPath_IsReflectedInTheScriptSrc()
    {
        // Arrange
        var options = new ScalarOptions();

        // Act
        options.WithBrokerClient(assetsPath: "/internal/broker");

        // Assert
        options.HeadContent.ShouldNotBeNull().ShouldContain("<script src=\"/internal/broker/plugin.js\"></script>");
    }

    [Fact]
    public void WithBrokerClient_PreservesExistingHeadContent()
    {
        // Arrange — another console (SignalR, gRPC) may already have registered on the same page.
        var options = new ScalarOptions { HeadContent = "<meta name=\"x\">" };

        // Act
        options.WithBrokerClient();

        // Assert
        options.HeadContent.ShouldNotBeNull().ShouldStartWith("<meta name=\"x\">");
        options.HeadContent.ShouldNotBeNull().ShouldContain("plugin.js");
    }
}
