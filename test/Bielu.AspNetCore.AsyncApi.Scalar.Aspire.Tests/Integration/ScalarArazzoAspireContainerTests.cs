using System.Collections.Concurrent;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Bielu.AspNetCore.Arazzo.Scalar.Aspire;
using Bielu.AspNetCore.Arazzo.Scalar.Tests.Browser;
using Bielu.AspNetCore.AsyncApi.Scalar.Aspire.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using Scalar.Aspire;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Bielu.AspNetCore.AsyncApi.Scalar.Aspire.Tests.Integration;

/// <summary>
/// End to end through the real Scalar.Aspire container: <c>WithArazzoOptions</c> writes the options into the
/// plugin module's URL, the container's Scalar loads that module through <c>pluginUrls</c>, and the module reads
/// the options back. The module is the locally built <c>dist/scalar-plugin.mjs</c>, served with the documents by a
/// file-based app Aspire runs (<c>Fixtures/ArazzoHost/arazzo-host.cs</c>) at a custom URL with a query parameter and
/// a fragment of its own, so the check needs no CDN release.
/// </summary>
public class ScalarArazzoAspireContainerTests(ITestOutputHelper output)
{
    [DockerFact]
    public async Task WithArazzoOptions_ContainerLoadsTheConfiguredModuleAndShowsPages()
    {
        if (BrowserAvailability.LaunchOptions is not { } launchOptions)
        {
            return; // No browser to drive: nothing to observe (the attribute already needs Docker).
        }

        // Arrange: Aspire runs the API (a file-based app serving the documents and the built plugin module) on a fixed
        // port, so the plugin URL can name it before the app starts.
        var port = FreePort();
        var pluginUrl = $"http://localhost:{port}/plugins/scalar-plugin.mjs?build=fixture#top";

        await using var builder = DistributedApplicationTestingBuilder.Create();
#pragma warning disable ASPIRECSHARPAPPS001 // File-based apps are in preview; this one is only a test fixture.
        var api = builder.AddCSharpApp("shop", Path.Combine(AppContext.BaseDirectory, "Fixtures", "ArazzoHost", "arazzo-host.cs"))
            .WithHttpEndpoint(port: port, targetPort: port, name: "http", isProxied: false)
            // Every interface, not only loopback: on Linux the Scalar container reaches the host through the Docker
            // bridge, which a loopback-only listener refuses (Docker Desktop forwards to loopback, so it hid this).
            .WithEnvironment("ASPNETCORE_URLS", "http://*:" + port)
            .WithEnvironment("PLUGIN_MODULE", FindBuiltModule())
            // A file-based app reports Running while `dotnet run` still builds it: wait until it really serves, or
            // the browser asks for the plugin module before anything listens and never asks again.
            .WithHttpHealthCheck("/openapi/v1.json");
#pragma warning restore ASPIRECSHARPAPPS001
        builder.AddScalarApiReference("scalar")
            .WithArazzoOptions(arazzo =>
            {
                arazzo.PluginUrl = pluginUrl;
                arazzo.EndLabelExtension = "x-test-end";
                arazzo.PagesExtension = "x-test-pages";
                arazzo.PageExtension = "x-test-page";
                arazzo.LabelExtension = "x-test-label";
            })
            .WithApiReference(api, (options, _) =>
            {
                options
                    .AddDocument("v1", "Shop", "/openapi/{documentName}.json")
                    .AddArazzoDocument("v1", "Workflows", isDefault: true);
                return Task.CompletedTask;
            });

        await using var app = await builder.BuildAsync();
        // The resources' own logs go to the test output, so a failure to start says why.
        var logger = app.Services.GetRequiredService<ResourceLoggerService>();
        foreach (var resource in new[] { "shop", "scalar" })
        {
            _ = Task.Run(async () =>
            {
                await foreach (var batch in logger.WatchAsync(resource))
                {
                    foreach (var line in batch)
                    {
                        output.WriteLine($"[{resource}] {line.Content}");
                    }
                }
            });
        }

        // Bounded, so a resource that never starts fails the test in minutes rather than hanging the run.
        using var startTimeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        await app.StartAsync(startTimeout.Token);
        await app.ResourceNotifications.WaitForResourceHealthyAsync("shop").WaitAsync(TimeSpan.FromMinutes(5));
        await app.ResourceNotifications.WaitForResourceHealthyAsync("scalar").WaitAsync(TimeSpan.FromMinutes(5));
        var scalarUrl = app.GetEndpoint("scalar", "http");

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(launchOptions);
        var page = await browser.NewPageAsync();
        var modules = new ConcurrentQueue<string>();
        page.Request += (_, request) =>
        {
            if (request.Url.Contains("/plugins/scalar-plugin.mjs", StringComparison.Ordinal))
            {
                modules.Enqueue(request.Url);
            }
        };

        page.Console += (_, message) => output.WriteLine($"[browser {message.Type}] {message.Text}");
        page.RequestFailed += (_, request) => output.WriteLine($"[browser request failed] {request.Url}: {request.Failure}");

        // Act
        await page.GotoAsync(scalarUrl.ToString());
        try
        {
            await page.Locator(".bielu-arazzo").WaitForAsync(new() { Timeout = 60_000 });
        }
        catch (TimeoutException)
        {
            // Say what the page did instead: which modules it asked for, and what it shows.
            output.WriteLine($"Plugin module requests: {string.Join(", ", modules)}");
            output.WriteLine($"Page {page.Url}: {await page.Locator("body").InnerTextAsync()}");
            throw;
        }
        await page.GetByRole(AriaRole.Button, new() { Name = "Pages", Exact = true }).First.ClickAsync();

        // Assert: the module the browser asked for is the configured one, with every option and the custom query.
        var requested = new Uri(modules.ShouldHaveSingleItem());
        var query = System.Web.HttpUtility.ParseQueryString(requested.Query);
        query["build"].ShouldBe("fixture");
        query["endLabelExtension"].ShouldBe("x-test-end");
        query["pagesExtension"].ShouldBe("x-test-pages");
        query["pageExtension"].ShouldBe("x-test-page");
        query["labelExtension"].ShouldBe("x-test-label");

        // ... and the module read them back: the Pages view shows the configured page and label.
        var overview = page.Locator(".bielu-arazzo .ba-overview").First;
        (await overview.Locator(".ba-diagram-band text").AllTextContentsAsync()).ShouldContain("Checkout page · /checkout");
        (await overview.Locator("[data-node-id='create'] > text").First.TextContentAsync()).ShouldBe("Place the order");
        (await overview.Locator(".ba-diagram-node-failure > text").First.TextContentAsync()).ShouldBe("Declined page");
    }

    private static int FreePort()
    {
        using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        return ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
    }

    /// <summary>
    /// The plugin module the npm package ships, built from this repository. Building
    /// <c>Bielu.AspNetCore.Arazzo.Scalar</c> (or the solution) produces it.
    /// </summary>
    private static string FindBuiltModule()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "src", "Bielu.AspNetCore.Arazzo.Scalar", "assets", "dist", "scalar-plugin.mjs");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException(
            "dist/scalar-plugin.mjs was not found: build src/Bielu.AspNetCore.Arazzo.Scalar first, which runs the npm build.");
    }
}
