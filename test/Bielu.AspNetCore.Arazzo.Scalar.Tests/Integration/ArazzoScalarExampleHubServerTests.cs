using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.Arazzo.Scalar.Tests.Integration;

/// <summary>The server the ArazzoScalar example's <c>updates</c> AsyncAPI document advertises its SignalR hub at.</summary>
public sealed class ArazzoScalarExampleHubServerTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Theory]
    [InlineData("urls", "http://localhost:6123", "localhost:6123")]
    [InlineData("urls", "http://localhost:6123;https://localhost:6124", "localhost:6123")]
    [InlineData("urls", "http://+:7000", "localhost:7000")]
    [InlineData("urls", "http://0.0.0.0:7001", "localhost:7001")]
    [InlineData("PublicUrl", "https://shop.example.com", "shop.example.com")]
    public async Task UpdatesDocument_ConfiguredAddress_AdvertisesItsAuthority(string key, string value, string authority)
    {
        // Arrange
        using var configured = factory.WithWebHostBuilder(builder => builder.UseSetting(key, value));

        // Act
        var host = await HostAsync(configured);

        // Assert
        host.ShouldBe(authority);
    }

    [Fact]
    public async Task UpdatesDocument_DefaultLaunchProfile_AdvertisesTheProfilesAddress()
    {
        // Arrange: the address `dotnet run` serves the app at.
        var contentRoot = factory.Services.GetRequiredService<IWebHostEnvironment>().ContentRootPath;
        var settings = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(contentRoot, "Properties", "launchSettings.json")))!;
        var applicationUrl = (string)settings["profiles"]!["http"]!["applicationUrl"]!;
        using var profile = factory.WithWebHostBuilder(builder => builder.UseSetting("urls", applicationUrl));

        // Act
        var host = await HostAsync(profile);

        // Assert: also the fallback when nothing is configured, as at build time.
        host.ShouldBe(new Uri(applicationUrl).Authority);
        AsyncOrder.DefaultAuthority.ShouldBe(host);
    }

    [Fact]
    public async Task UpdatesDocument_RunningOnAnotherPort_AdvertisesAnAddressThatReachesTheHub()
    {
        // Arrange: a real server on a free port, rather than the launch profile's.
        await using var running = new WebApplicationFactory<Program>();
        running.UseKestrel(0);
        running.StartServer();

        // Act
        var host = await HostAsync(running);
        using var http = new HttpClient();
        using var negotiate = await http.PostAsync(new Uri($"http://{host}{AsyncOrder.HubPath}/negotiate?negotiateVersion=1"), content: null);

        // Assert
        host.ShouldNotBe(AsyncOrder.DefaultAuthority);
        negotiate.IsSuccessStatusCode.ShouldBeTrue();
        JsonNode.Parse(await negotiate.Content.ReadAsStringAsync())!["connectionId"].ShouldNotBeNull();
    }

    private static async Task<string> HostAsync(WebApplicationFactory<Program> app)
    {
        using var client = app.CreateClient();
        var document = JsonNode.Parse(await client.GetStringAsync(new Uri("/asyncapi/updates.json", UriKind.Relative)))!;
        return (string)document["servers"]!["signalr"]!["host"]!;
    }
}
