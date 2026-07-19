using Scalar.AspNetCore;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Scalar.Grpc.Tests;

public class ScalarGrpcOptionsExtensionsTests
{
    [Fact]
    public void WithGrpcClient_InjectsPluginScript()
    {
        // Arrange
        var options = new ScalarOptions();

        // Act
        options.WithGrpcClient();

        // Assert
        options.HeadContent.ShouldNotBeNull();
        options.HeadContent.ShouldContain("src=\"/bielu/scalar/grpc/plugin.js\"");
        // No explicit documents → no global override; the bundle auto-discovers from the config.
        options.HeadContent.ShouldNotContain("__BIELU_SCALAR_GRPC__");
    }

    [Fact]
    public void WithGrpcClient_WithDocuments_InjectsGlobalOverride()
    {
        // Arrange
        var options = new ScalarOptions();

        // Act
        options.WithGrpcClient(grpc => grpc.AddDocument("grpc", "/asyncapi/grpc.json"));

        // Assert
        options.HeadContent.ShouldNotBeNull();
        options.HeadContent.ShouldContain("window.__BIELU_SCALAR_GRPC__");
        options.HeadContent.ShouldContain("/asyncapi/grpc.json");
    }

    [Fact]
    public void WithGrpcClient_CustomAssetsPath_IsUsedForTheScriptTag()
    {
        // Arrange
        var options = new ScalarOptions();

        // Act
        options.WithGrpcClient(assetsPath: "/custom/grpc");

        // Assert
        options.HeadContent.ShouldNotBeNull();
        options.HeadContent.ShouldContain("src=\"/custom/grpc/plugin.js\"");
    }
}
