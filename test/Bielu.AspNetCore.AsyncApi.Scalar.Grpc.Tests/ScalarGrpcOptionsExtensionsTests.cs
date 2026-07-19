using Scalar.AspNetCore;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Scalar.Grpc.Tests;

public class ScalarGrpcOptionsExtensionsTests
{
    [Fact]
    public void WithGrpcClient_InjectsPluginScript()
    {
        var options = new ScalarOptions();

        options.WithGrpcClient();

        options.HeadContent.ShouldNotBeNull();
        options.HeadContent.ShouldContain("src=\"/bielu/scalar/grpc/plugin.js\"");
        // No explicit documents → no global override; the bundle auto-discovers from the config.
        options.HeadContent.ShouldNotContain("__BIELU_SCALAR_GRPC__");
    }

    [Fact]
    public void WithGrpcClient_WithDocuments_InjectsGlobalOverride()
    {
        var options = new ScalarOptions();

        options.WithGrpcClient(grpc => grpc.AddDocument("grpc", "/asyncapi/grpc.json"));

        options.HeadContent.ShouldNotBeNull();
        options.HeadContent.ShouldContain("window.__BIELU_SCALAR_GRPC__");
        options.HeadContent.ShouldContain("/asyncapi/grpc.json");
    }

    [Fact]
    public void WithGrpcClient_CustomAssetsPath_IsUsedForTheScriptTag()
    {
        var options = new ScalarOptions();

        options.WithGrpcClient(assetsPath: "/custom/grpc");

        options.HeadContent.ShouldNotBeNull();
        options.HeadContent.ShouldContain("src=\"/custom/grpc/plugin.js\"");
    }
}
