using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Playwright;
using Scalar.AspNetCore;
using Xunit;

namespace Bielu.AspNetCore.Arazzo.Scalar.Tests.Browser;

/// <summary>
/// A Scalar page on a real port, with an OpenAPI, an AsyncAPI and an Arazzo document, and a headless
/// Chromium to drive it. The Arazzo document is the <c>ArazzoScalar</c> example's field reference, which
/// uses every Arazzo 1.1 field; the OpenAPI and AsyncAPI documents are static fixtures holding the
/// operations and channels its steps target.
/// </summary>
public sealed class ScalarArazzoBrowserFixture : IAsyncLifetime
{
    private WebApplication? _app;
    private IPlaywright? _playwright;

    public IBrowser Browser { get; private set; } = null!;

    public string BaseAddress { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        if (BrowserAvailability.LaunchOptions is not { } launchOptions)
        {
            return;
        }

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var app = builder.Build();

        MapFixture(app, "/openapi/v1.json", "orders.openapi.json");
        MapFixture(app, "/asyncapi/v1.json", "orders.asyncapi.json");
        MapFixture(app, "/fixtures/field-reference.arazzo.json", "field-reference.arazzo.json");
        app.MapScalarArazzoAssets();
        app.MapScalarApiReference(options => options
            .AddDocument("orders", "Orders", "/openapi/v1.json")
            .AddDocument("events", "Events", "/asyncapi/v1.json")
            .AddDocument("field-reference", "Arazzo field reference", "/fixtures/field-reference.arazzo.json")
            .WithArazzo());

        await app.StartAsync();
        _app = app;
        // Port 0 binds a free port; the real address is known once the server has started.
        BaseAddress = app.Urls.First();

        _playwright = await Playwright.CreateAsync();
        Browser = await _playwright.Chromium.LaunchAsync(launchOptions);
    }

    public async Task DisposeAsync()
    {
        if (Browser is not null)
        {
            await Browser.CloseAsync();
        }

        _playwright?.Dispose();
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    /// <summary>A fresh page on the Arazzo document, once its workflows have rendered.</summary>
    public async Task<IPage> OpenArazzoDocumentAsync(IList<string>? consoleErrors = null)
    {
        var page = await Browser.NewPageAsync(new() { ViewportSize = new() { Width = 1400, Height = 900 } });
        page.Console += (_, message) =>
        {
            if (message.Type == "error")
            {
                consoleErrors?.Add(message.Text);
            }
        };
        page.PageError += (_, error) => consoleErrors?.Add(error);

        // Everything the page logged or failed to load, so a timeout says why instead of only that.
        var log = new List<string>();
        page.Console += (_, message) => log.Add($"{message.Type}: {message.Text}");
        page.RequestFailed += (_, request) => log.Add($"failed: {request.Url} {request.Failure}");

        // Scalar picks the document from the first hash segment: the slug of the source's title.
        await page.GotoAsync($"{BaseAddress}/scalar/#arazzo-field-reference");
        try
        {
            // Step targets resolve once Scalar has also loaded the OpenAPI and AsyncAPI documents, which
            // it does after rendering the Arazzo one: wait until both sources show as loaded.
            await page.WaitForFunctionAsync(
                "() => [...document.querySelectorAll('.bielu-arazzo .ba-sources tr')].filter(row => !row.textContent.toLowerCase().includes('not loaded')).length === 2",
                null,
                new() { Timeout = 15000 });
        }
        catch (TimeoutException)
        {
            var shown = await page.EvaluateAsync<string>("() => location.href + '\\n' + document.body.innerText.slice(0, 1500)");
            throw new TimeoutException($"The Arazzo document did not finish loading.\n{shown}\n---\n{string.Join('\n', log)}");
        }

        return page;
    }

    private static void MapFixture(WebApplication app, string route, string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName);
        app.MapGet(route, () => Results.File(path, "application/json"));
    }
}
