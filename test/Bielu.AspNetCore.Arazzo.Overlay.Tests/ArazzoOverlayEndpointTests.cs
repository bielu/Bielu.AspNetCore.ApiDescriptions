using System.Net;
using System.Text.Json;
using Bielu.AspNetCore.Arazzo.Extensions;
using Bielu.AspNetCore.Arazzo.Services;
using Bielu.Overlay.Models;
using Bielu.Overlay.Readers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.Arazzo.Overlay.Tests;

/// <summary>
/// Integration tests proving <c>MapArazzo()</c> serves an already-overlaid workflow document. The Overlay
/// Specification is normatively OpenAPI-scoped, so this is an extension the library offers rather than a
/// conformance claim — but the mechanism is the same one, over the same JSON tree.
/// </summary>
public class ArazzoOverlayEndpointTests
{
    private const string RetitleOverlay = """
        overlay: 1.1.0
        info:
          title: Retitle workflows
          version: 1.0.0
        actions:
          - target: $.info
            update:
              title: Overlaid Workflows
        """;

    private const string RemoveWorkflowOverlay = """
        overlay: 1.1.0
        info:
          title: Strip internal workflow
          version: 1.0.0
        actions:
          - target: $.workflows[?@.workflowId == 'internal']
            remove: true
        """;

    private const string NoMatchOverlay = """
        overlay: 1.1.0
        info:
          title: Targets nothing
          version: 1.0.0
        actions:
          - target: $.thisDoesNotExist
            update:
              title: never applied
        """;

    private static string GetDocumentRoute(string documentName) =>
        ArazzoDefaults.DefaultArazzoRoute.Replace("{documentName}", documentName);

    [Fact]
    public async Task JsonEndpoint_WithOverlay_ServesOverlaidDocument()
    {
        // Arrange
        using var host = await CreateTestHostAsync(options => options.AddOverlay(Parse(RetitleOverlay)));
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync(GetDocumentRoute(ArazzoDefaults.DefaultDocumentName));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("info").GetProperty("title").GetString().ShouldBe("Overlaid Workflows");
    }

    [Fact]
    public async Task JsonEndpoint_WithFilterTargetedRemove_StripsTheMatchingWorkflow()
    {
        // Arrange — an RFC 9535 filter over an array of objects, the shape the spec's own examples use.
        using var host = await CreateTestHostAsync(options => options.AddOverlay(Parse(RemoveWorkflowOverlay)));
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync(GetDocumentRoute(ArazzoDefaults.DefaultDocumentName));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var workflowIds = document.RootElement.GetProperty("workflows")
            .EnumerateArray()
            .Select(w => w.GetProperty("workflowId").GetString())
            .ToList();
        workflowIds.ShouldContain("public");
        workflowIds.ShouldNotContain("internal");
    }

    [Fact]
    public async Task YamlEndpoint_WithOverlay_ServesOverlaidYaml()
    {
        // Arrange
        using var host = await CreateTestHostAsync(
            options => options.AddOverlay(Parse(RetitleOverlay)),
            _ => "/arazzo/{documentName}.yaml");
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync($"/arazzo/{ArazzoDefaults.DefaultDocumentName}.yaml");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/vnd.oai.workflows+yaml");

        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("Overlaid Workflows");
        body.TrimStart().ShouldNotStartWith("{");
    }

    [Fact]
    public async Task MultipleOverlays_ApplyInRegistrationOrder()
    {
        // Arrange
        const string secondOverlay = """
            overlay: 1.1.0
            info:
              title: Retitle again
              version: 1.0.0
            actions:
              - target: $.info
                update:
                  title: Second Overlay Wins
            """;

        using var host = await CreateTestHostAsync(options => options
            .AddOverlay(Parse(RetitleOverlay))
            .AddOverlay(Parse(secondOverlay)));
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync(GetDocumentRoute(ArazzoDefaults.DefaultDocumentName));

        // Assert
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("info").GetProperty("title").GetString().ShouldBe("Second Overlay Wins");
    }

    [Fact]
    public async Task ZeroMatchTarget_WithStrict_FailsTheRequest()
    {
        // Arrange
        using var host = await CreateTestHostAsync(options => options
            .AddOverlay(Parse(NoMatchOverlay))
            .ConfigureOverlays(apply => apply.Strict = true));
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync(GetDocumentRoute(ArazzoDefaults.DefaultDocumentName));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task ZeroMatchTarget_WithoutStrict_ServesDocumentUnchanged()
    {
        // Arrange
        using var host = await CreateTestHostAsync(options => options.AddOverlay(Parse(NoMatchOverlay)));
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync(GetDocumentRoute(ArazzoDefaults.DefaultDocumentName));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("info").GetProperty("title").GetString().ShouldBe("Test Workflows");
    }

    [Fact]
    public async Task NoOverlayRegistered_ServesDocumentUnchanged()
    {
        // Arrange
        using var host = await CreateTestHostAsync();
        var client = host.GetTestClient();

        // Act
        var response = await client.GetAsync(GetDocumentRoute(ArazzoDefaults.DefaultDocumentName));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("info").GetProperty("title").GetString().ShouldBe("Test Workflows");
    }

    [Fact]
    public async Task Overlay_ChangesTheETag()
    {
        // Arrange — the ETag is computed from the served bytes, so it must reflect the transformation.
        using var plainHost = await CreateTestHostAsync();
        using var overlaidHost = await CreateTestHostAsync(options => options.AddOverlay(Parse(RetitleOverlay)));

        // Act
        var plain = await plainHost.GetTestClient().GetAsync(GetDocumentRoute(ArazzoDefaults.DefaultDocumentName));
        var overlaid = await overlaidHost.GetTestClient().GetAsync(GetDocumentRoute(ArazzoDefaults.DefaultDocumentName));

        // Assert
        plain.Headers.ETag.ShouldNotBeNull();
        overlaid.Headers.ETag.ShouldNotBeNull();
        overlaid.Headers.ETag!.Tag.ShouldNotBe(plain.Headers.ETag!.Tag);
    }

    private static OverlayDocument Parse(string yaml)
    {
        var read = OverlayStringReader.Read(yaml);
        read.Document.ShouldNotBeNull();
        return read.Document!;
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
                        options.AddWorkflow("public", wf => wf.Step("only", s => s.Workflow("public")));
                        options.AddWorkflow("internal", wf => wf.Step("only", s => s.Workflow("internal")));
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
