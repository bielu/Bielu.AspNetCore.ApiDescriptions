using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Scalar.AspNetCore;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Scalar.SignalR.Tests;

public class ScalarSignalROptionsExtensionsTests
{
    [Fact]
    public void WithSignalRClient_InjectsPluginScript()
    {
        // Arrange
        var options = new ScalarOptions();

        // Act
        var returned = options.WithSignalRClient();

        // Assert
        returned.ShouldBeSameAs(options);
        options.HeadContent.ShouldNotBeNull();
        options.HeadContent.ShouldContain("src=\"/bielu/scalar/signalr/plugin.js\"");
        // No explicit documents → no global override; the bundle auto-discovers from the config.
        options.HeadContent.ShouldNotContain("__BIELU_SCALAR_SIGNALR__");
    }

    [Fact]
    public void WithSignalRClient_WithDocuments_InjectsGlobalOverride()
    {
        // Arrange
        var options = new ScalarOptions();

        // Act
        options.WithSignalRClient(signalR => signalR.AddDocument("chat", "/asyncapi/chat.json"));

        // Assert
        options.HeadContent.ShouldNotBeNull();
        options.HeadContent.ShouldContain("window.__BIELU_SCALAR_SIGNALR__");
        options.HeadContent.ShouldContain("/asyncapi/chat.json");
    }

    [Fact]
    public void WithSignalRClient_CustomAssetsPath_IsUsedForTheScriptTag()
    {
        // Arrange
        var options = new ScalarOptions();

        // Act
        options.WithSignalRClient(assetsPath: "/custom/signalr");

        // Assert
        options.HeadContent.ShouldNotBeNull();
        options.HeadContent.ShouldContain("src=\"/custom/signalr/plugin.js\"");
    }

    [Fact]
    public void WithSignalRClient_InvalidArguments_Throw()
    {
        Should.Throw<ArgumentNullException>(() => ((ScalarOptions)null!).WithSignalRClient());
        Should.Throw<ArgumentException>(() => new ScalarOptions().WithSignalRClient(assetsPath: string.Empty));
    }
}

/// <summary>
/// Boots the SignalRChat example application, which calls <c>MapScalarSignalRAssets()</c>, and checks the
/// console bundle it serves.
/// </summary>
public class ScalarSignalREndpointTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task MapScalarSignalRAssets_PluginBundle_IsServed()
    {
        // Arrange
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/bielu/scalar/signalr/plugin.js");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("text/javascript");
        // The bundle registers the console custom element; its tag is a stable marker.
        (await response.Content.ReadAsStringAsync()).ShouldContain("bielu-signalr-console");
    }
}
