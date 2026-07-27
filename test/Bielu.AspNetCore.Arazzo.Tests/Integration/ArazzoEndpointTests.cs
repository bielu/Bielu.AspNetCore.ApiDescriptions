using System.Net;
using System.Text.Json;
using Bielu.AspNetCore.Arazzo.Extensions;
using Bielu.AspNetCore.Arazzo.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.Arazzo.Tests.Integration;

public class ArazzoEndpointTests
{
    private static string GetDocumentRoute(string documentName) =>
        ArazzoDefaults.DefaultArazzoRoute.Replace("{documentName}", documentName);

    [Fact]
    public async Task MapArazzo_DefaultRoute_ReturnsDocument()
    {
        using var host = await CreateTestHostAsync();
        var client = host.GetTestClient();

        var response = await client.GetAsync(GetDocumentRoute(ArazzoDefaults.DefaultDocumentName));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/vnd.oai.workflows+json");

        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("arazzo").GetString().ShouldBe("1.1.0");
        document.RootElement.GetProperty("info").GetProperty("title").GetString().ShouldBe("Test Workflows");
    }

    [Fact]
    public async Task MapArazzo_YamlRoute_ReturnsYamlContentType()
    {
        using var host = await CreateTestHostAsync(configureEndpoint: _ => "/arazzo/{documentName}.yaml");
        var client = host.GetTestClient();

        var response = await client.GetAsync($"/arazzo/{ArazzoDefaults.DefaultDocumentName}.yaml");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/vnd.oai.workflows+yaml");
    }

    [Fact]
    public async Task MapArazzo_NonExistentDocument_Returns404()
    {
        using var host = await CreateTestHostAsync();
        var client = host.GetTestClient();

        var response = await client.GetAsync("/arazzo/unknown.json");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MapArazzo_RepeatedRequests_ProduceSameETag()
    {
        using var host = await CreateTestHostAsync();
        var client = host.GetTestClient();

        var response1 = await client.GetAsync(GetDocumentRoute(ArazzoDefaults.DefaultDocumentName));
        var response2 = await client.GetAsync(GetDocumentRoute(ArazzoDefaults.DefaultDocumentName));

        response1.Headers.ETag.ShouldNotBeNull();
        response1.Headers.ETag!.Tag.ShouldBe(response2.Headers.ETag!.Tag);
    }

    [Fact]
    public async Task MapArazzo_ConditionalRequestWithMatchingETag_Returns304()
    {
        using var host = await CreateTestHostAsync();
        var client = host.GetTestClient();

        var response1 = await client.GetAsync(GetDocumentRoute(ArazzoDefaults.DefaultDocumentName));
        response1.Headers.ETag.ShouldNotBeNull();

        using var request = new HttpRequestMessage(HttpMethod.Get, GetDocumentRoute(ArazzoDefaults.DefaultDocumentName));
        request.Headers.IfNoneMatch.Add(response1.Headers.ETag!);
        var response2 = await client.SendAsync(request);

        response2.StatusCode.ShouldBe(HttpStatusCode.NotModified);
    }

    [Fact]
    public async Task MapArazzo_ConditionalRequestWithWeakEquivalentETag_Returns304()
    {
        using var host = await CreateTestHostAsync();
        var client = host.GetTestClient();

        var response1 = await client.GetAsync(GetDocumentRoute(ArazzoDefaults.DefaultDocumentName));
        response1.Headers.ETag.ShouldNotBeNull();

        // If-None-Match uses weak comparison (RFC 9110 §13.1.2): a weak validator with the same opaque
        // tag as our strong ETag must still count as a match.
        using var request = new HttpRequestMessage(HttpMethod.Get, GetDocumentRoute(ArazzoDefaults.DefaultDocumentName));
        request.Headers.IfNoneMatch.Add(new System.Net.Http.Headers.EntityTagHeaderValue(response1.Headers.ETag!.Tag, isWeak: true));
        var response2 = await client.SendAsync(request);

        response2.StatusCode.ShouldBe(HttpStatusCode.NotModified);
    }

    [Fact]
    public async Task MapArazzo_ConditionalRequestWithWildcard_Returns304()
    {
        using var host = await CreateTestHostAsync();
        var client = host.GetTestClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, GetDocumentRoute(ArazzoDefaults.DefaultDocumentName));
        request.Headers.TryAddWithoutValidation("If-None-Match", "*");
        var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.NotModified);
    }

    [Fact]
    public async Task MapArazzo_ConditionalRequestWithTagInMultiValueList_Returns304()
    {
        using var host = await CreateTestHostAsync();
        var client = host.GetTestClient();

        var response1 = await client.GetAsync(GetDocumentRoute(ArazzoDefaults.DefaultDocumentName));
        response1.Headers.ETag.ShouldNotBeNull();

        using var request = new HttpRequestMessage(HttpMethod.Get, GetDocumentRoute(ArazzoDefaults.DefaultDocumentName));
        request.Headers.TryAddWithoutValidation("If-None-Match", $"\"bogus\", {response1.Headers.ETag}");
        var response2 = await client.SendAsync(request);

        response2.StatusCode.ShouldBe(HttpStatusCode.NotModified);
    }

    [Fact]
    public async Task MapArazzo_ConditionalRequestWithNonMatchingETag_ReturnsFullBody()
    {
        using var host = await CreateTestHostAsync();
        var client = host.GetTestClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, GetDocumentRoute(ArazzoDefaults.DefaultDocumentName));
        request.Headers.IfNoneMatch.Add(new System.Net.Http.Headers.EntityTagHeaderValue("\"bogus\""));
        var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.ETag.ShouldNotBeNull();
    }

    private static async Task<IHost> CreateTestHostAsync(
        Action<ArazzoOptions>? configureOptions = null,
        Func<string, string>? configureEndpoint = null)
    {
        var pattern = configureEndpoint?.Invoke(ArazzoDefaults.DefaultArazzoRoute) ?? ArazzoDefaults.DefaultArazzoRoute;

        var builder = Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddArazzo(options =>
                    {
                        options.WithInfo("Test Workflows", "1.0.0");
                        options.ValidateSourceReferencesOnStartup = false;
                        options.AddWorkflow("noop", wf => wf
                            .Step("only", s => s.Workflow("noop")));
                        configureOptions?.Invoke(options);
                    });
                    services.AddRouting();
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapArazzo(pattern));
                });
            });

        var host = builder.Build();
        await host.StartAsync();
        return host;
    }
}
