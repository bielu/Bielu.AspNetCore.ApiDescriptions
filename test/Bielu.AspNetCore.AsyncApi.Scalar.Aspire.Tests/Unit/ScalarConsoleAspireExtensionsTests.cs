using System.Text;
using System.Text.Json;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Bielu.AspNetCore.AsyncApi.Scalar.Grpc.Aspire;
using Bielu.AspNetCore.AsyncApi.Scalar.SignalR.Aspire;
using global::Scalar.Aspire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Scalar.Aspire.Tests.Unit;

/// <summary>
/// Unit tests for the <c>WithGrpcClient</c> and <c>WithSignalRClient</c> AppHost extensions. They only shape the
/// Aspire app model, so nothing is started: the tests read the plugin URLs back from the Scalar resource's options.
/// </summary>
public class ScalarConsoleAspireExtensionsTests
{
    private static async Task<IList<string>> GetPluginUrlsAsync(Action<IResourceBuilder<ScalarResource>> configure)
    {
        await using var builder = DistributedApplicationTestingBuilder.Create();
        var scalar = builder.AddScalarApiReference();
        configure(scalar);

        await using var provider = builder.Services.BuildServiceProvider();
        var pluginUrls = provider.GetRequiredService<IOptionsMonitor<ScalarAspireOptions>>().Get(scalar.Resource.Name).PluginUrls;
        pluginUrls.ShouldNotBeNull();
        return pluginUrls;
    }

    private static JsonElement DecodeDocuments(string pluginUrl)
    {
        var encoded = pluginUrl[(pluginUrl.IndexOf("documents=", StringComparison.Ordinal) + "documents=".Length)..];
        var json = Encoding.UTF8.GetString(Convert.FromBase64String(Uri.UnescapeDataString(encoded)));
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    [Fact]
    public async Task WithGrpcClient_WithoutDocuments_AddsTheDefaultPluginUrl()
    {
        // Act
        var pluginUrls = await GetPluginUrlsAsync(scalar => scalar.WithGrpcClient());

        // Assert
        pluginUrls.ShouldContain(ScalarGrpcAspireExtensions.DefaultPluginUrl);
        ScalarGrpcAspireExtensions.DefaultPluginUrl.ShouldStartWith("https://cdn.jsdelivr.net/npm/@bielu/scalar-grpc@");
    }

    [Fact]
    public async Task WithGrpcClient_WithDocuments_EncodesThemOnTheModuleUrl()
    {
        // Act
        var pluginUrls = await GetPluginUrlsAsync(scalar => scalar.WithGrpcClient(options =>
            options.AddDocument("greeter", "https://api.example.com/asyncapi/grpc.json")));

        // Assert
        var pluginUrl = pluginUrls.ShouldHaveSingleItem();
        pluginUrl.ShouldStartWith(ScalarGrpcAspireExtensions.DefaultPluginUrl + "?documents=");
        var document = DecodeDocuments(pluginUrl).EnumerateArray().ShouldHaveSingleItem();
        document.GetProperty("name").GetString().ShouldBe("greeter");
        document.GetProperty("url").GetString().ShouldBe("https://api.example.com/asyncapi/grpc.json");
    }

    [Fact]
    public async Task WithGrpcClient_CustomUrlWithQuery_AppendsDocumentsWithAmpersand()
    {
        // Act
        var pluginUrls = await GetPluginUrlsAsync(scalar => scalar.WithGrpcClient(
            options => options.AddDocument("greeter", "/asyncapi/grpc.json"),
            pluginUrl: "https://cdn.example.com/plugin.mjs?v=1"));

        // Assert
        pluginUrls.ShouldHaveSingleItem().ShouldStartWith("https://cdn.example.com/plugin.mjs?v=1&documents=");
    }

    [Fact]
    public async Task WithSignalRClient_WithoutDocuments_AddsTheDefaultPluginUrl()
    {
        // Act
        var pluginUrls = await GetPluginUrlsAsync(scalar => scalar.WithSignalRClient());

        // Assert
        pluginUrls.ShouldContain(ScalarSignalRAspireExtensions.DefaultPluginUrl);
        ScalarSignalRAspireExtensions.DefaultPluginUrl.ShouldStartWith("https://cdn.jsdelivr.net/npm/@bielu/scalar-signalr@");
    }

    [Fact]
    public async Task WithSignalRClient_WithDocuments_EncodesThemOnTheModuleUrl()
    {
        // Act
        var pluginUrls = await GetPluginUrlsAsync(scalar => scalar.WithSignalRClient(options => options
            .AddDocument("chat", "https://api.example.com/asyncapi/chat.json")
            .AddDocument("notifications", "https://api.example.com/asyncapi/notifications.json")));

        // Assert
        var documents = DecodeDocuments(pluginUrls.ShouldHaveSingleItem()).EnumerateArray().ToList();
        documents.Select(document => document.GetProperty("name").GetString()).ShouldBe(["chat", "notifications"]);
    }

    [Fact]
    public async Task WithSignalRClient_CustomUrlWithQuery_AppendsDocumentsWithAmpersand()
    {
        // Act
        var pluginUrls = await GetPluginUrlsAsync(scalar => scalar.WithSignalRClient(
            options => options.AddDocument("chat", "/asyncapi/chat.json"),
            pluginUrl: "https://cdn.example.com/signalr.mjs?v=2"));

        // Assert
        pluginUrls.ShouldHaveSingleItem().ShouldStartWith("https://cdn.example.com/signalr.mjs?v=2&documents=");
    }

    [Fact]
    public void WithClient_InvalidArguments_Throw()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => ScalarGrpcAspireExtensions.WithGrpcClient(null!));
        Should.Throw<ArgumentNullException>(() => ScalarSignalRAspireExtensions.WithSignalRClient(null!));
    }

    [Fact]
    public async Task WithClient_EmptyPluginUrl_Throws()
    {
        // Arrange
        await using var builder = DistributedApplicationTestingBuilder.Create();
        var scalar = builder.AddScalarApiReference();

        // Act & Assert
        Should.Throw<ArgumentException>(() => scalar.WithGrpcClient(pluginUrl: ""));
        Should.Throw<ArgumentException>(() => scalar.WithSignalRClient(pluginUrl: ""));
    }

    [Theory]
    [InlineData("", "https://example.com/doc.json")]
    [InlineData("  ", "https://example.com/doc.json")]
    [InlineData("doc", "")]
    public void AddDocument_BlankNameOrUrl_Throws(string name, string url)
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => new ScalarGrpcAspireOptions().AddDocument(name, url));
        Should.Throw<ArgumentException>(() => new ScalarSignalRAspireOptions().AddDocument(name, url));
    }
}
