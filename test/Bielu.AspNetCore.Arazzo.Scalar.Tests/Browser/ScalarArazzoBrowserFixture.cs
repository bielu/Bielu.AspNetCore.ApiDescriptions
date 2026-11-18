using System.Text.Json.Nodes;
using Bielu.Arazzo.Models;
using Bielu.AspNetCore.Arazzo.Extensions;
using Bielu.AspNetCore.Arazzo.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using Scalar.AspNetCore;
using Xunit;

namespace Bielu.AspNetCore.Arazzo.Scalar.Tests.Browser;

/// <summary>
/// A Scalar page on a real port, with an OpenAPI, an AsyncAPI and an Arazzo document, and a headless
/// Chromium to drive it. The Arazzo document is the <c>ArazzoScalar</c> example's field reference, which
/// uses every Arazzo 1.1 field; the OpenAPI and AsyncAPI documents are static fixtures holding the
/// operations and channels its steps target. A second Arazzo document is built with the fluent API, so the
/// authoring path is checked end to end as well.
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
        builder.Services.AddArazzo(GeneratedDocumentName, ConfigureGeneratedDocument);
        builder.Services.AddArazzo(AccountDocumentName, ConfigureAccountDocument);
        var app = builder.Build();

        MapFixture(app, "/openapi/v1.json", "orders.openapi.json");
        MapFixture(app, "/asyncapi/v1.json", "orders.asyncapi.json");
        MapFixture(app, "/fixtures/field-reference.arazzo.json", "field-reference.arazzo.json");
        app.MapArazzo();
        app.MapScalarArazzoAssets();
        app.MapScalarApiReference(options => options
            .AddDocument("orders", "Orders", "/openapi/v1.json")
            .AddDocument("events", "Events", "/asyncapi/v1.json")
            .AddDocument("field-reference", "Arazzo field reference", "/fixtures/field-reference.arazzo.json")
            .AddArazzoDocument(GeneratedDocumentName, "Generated workflows")
            .AddArazzoDocument(AccountDocumentName, "Account workflows")
            .WithArazzoOptions(arazzo =>
            {
                arazzo.EndLabelExtension = EndLabelExtension;
                arazzo.PagesExtension = PagesExtension;
                arazzo.PageExtension = EndLabelExtension;
                arazzo.LabelExtension = LabelExtension;
            }));

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

    /// <summary>The name of the Arazzo document built with the fluent API, served by <c>MapArazzo</c>.</summary>
    public const string GeneratedDocumentName = "generated";

    /// <summary>Scalar's hash slug for <see cref="GeneratedDocumentName"/>, from its title.</summary>
    public const string GeneratedDocumentSlug = "generated-workflows";

    public const string AccountDocumentName = "account";

    public const string AccountDocumentSlug = "account-workflows";

    /// <summary>The extension whose value names the end nodes of the access document's end actions.</summary>
    public const string EndLabelExtension = "x-page";

    /// <summary>The account document's map of pages, for the Pages view.</summary>
    public const string PagesExtension = "x-shop-pages";

    /// <summary>The plain-language labels of the account document's steps and actions, for the Pages view.</summary>
    public const string LabelExtension = "x-label";

    /// <summary>
    /// Workflows of a shop that signs its customers in: every API step retries through the signIn workflow on
    /// 401 and ends on a page (named by <see cref="EndLabelExtension"/>) on 403 or any other failure.
    /// </summary>
    private static void ConfigureAccountDocument(ArazzoOptions options)
    {
        options.ValidateSourceReferencesOnStartup = false;
        // Both sources, like the other documents, so the page is ready once both show as loaded.
        options.WithInfo("Account workflows", "1.0.0")
            .AddSourceDescription("orders", "/openapi/v1.json", ArazzoSourceDescriptionType.OpenApi)
            .AddSourceDescription("events", "/asyncapi/v1.json", ArazzoSourceDescriptionType.AsyncApi)
            .WithExtension(PagesExtension, new JsonObject
            {
                ["signIn"] = new JsonObject { ["title"] = "Sign in", ["route"] = "/sign-in" },
                ["orders"] = new JsonObject { ["title"] = "Order list", ["route"] = "/orders" },
                ["forbidden"] = new JsonObject { ["title"] = "No permission", ["route"] = "/forbidden" },
                ["error"] = new JsonObject { ["title"] = "Something went wrong", ["route"] = "/error" },
            });

        options.AddWorkflow("signIn", workflow => workflow
            .WithExtension(LabelExtension, "Sign the customer in")
            .Step("authorize", step => step.Operation("createOrder").WithExtension(EndLabelExtension, "signIn").WithExtension(LabelExtension, "Open the sign-in page"))
            .Step("exchangeCode", step => step.Operation("getOrder").WithExtension(EndLabelExtension, "signIn")));
        options.AddWorkflow("loadAccount", workflow => workflow
            .WithExtension(LabelExtension, "Load the customer account")
            .Step("getAccount", step => AddFailures(step.Operation("getOrder").SuccessCriteria("$statusCode == 200").WithExtension(LabelExtension, "Load the account"))));
        options.AddWorkflow("browseOrders", workflow => workflow
            .Step("loadAccount", step => step.Workflow("loadAccount"))
            .Step("listOrders", step => AddFailures(step.Operation("getOrder").WithExtension(EndLabelExtension, "orders").WithExtension(LabelExtension, "Load the order list"))));
    }

    private static ArazzoStepBuilder AddFailures(ArazzoStepBuilder step) => step
        .OnFailure(new ArazzoFailureAction
        {
            Name = "signIn",
            Type = ArazzoFailureActionType.Retry,
            WorkflowId = "signIn",
            RetryLimit = 1,
            Criteria = [new ArazzoCriterion { Condition = "$statusCode == 401" }],
        })
        .OnFailure(new ArazzoFailureAction
        {
            Name = "showForbiddenPage",
            Type = ArazzoFailureActionType.End,
            Criteria = [new ArazzoCriterion { Condition = "$statusCode == 403" }],
            Extensions = new Dictionary<string, JsonNode?> { [EndLabelExtension] = JsonValue.Create("forbidden"), [LabelExtension] = JsonValue.Create("No permission") },
        })
        .OnFailure(new ArazzoFailureAction
        {
            Name = "showErrorPage",
            Type = ArazzoFailureActionType.End,
            Extensions = new Dictionary<string, JsonNode?> { [EndLabelExtension] = JsonValue.Create("error") },
        });

    /// <summary>
    /// A document built with <c>AddArazzo</c>'s fluent API rather than written by hand, so the tests cover the
    /// path from authoring to rendering: payload replacements, selector-valued parameters and outputs, and a
    /// workflow output a later workflow consumes.
    /// </summary>
    private static void ConfigureGeneratedDocument(ArazzoOptions options)
    {
        // The sources are the static fixtures, which are not documents this app generates.
        options.ValidateSourceReferencesOnStartup = false;
        options.WithInfo("Generated workflows", "1.0.0")
            .AddSourceDescription("orders", "/openapi/v1.json", ArazzoSourceDescriptionType.OpenApi)
            .AddSourceDescription("events", "/asyncapi/v1.json", ArazzoSourceDescriptionType.AsyncApi)
            .AddComponentFailureAction("giveUp", new ArazzoFailureAction { Name = "giveUp", Type = ArazzoFailureActionType.End });

        options.AddWorkflow("placeOrder", workflow => workflow
            .Step("create", step => step
                .Operation("createOrder")
                .Payload(new JsonObject { ["sku"] = null, ["quantity"] = 1 }, "application/json")
                .Replacement("/sku", "$inputs.sku")
                .Replacement("$.quantity", ArazzoValue.FromSelector(new ArazzoSelector
                {
                    Context = "$inputs.cart",
                    Selector = "$.lines[0].quantity",
                    Type = "jsonpath",
                }), "jsonpath")
                .Output("orderId", new ArazzoSelector { Context = "$response.body", Selector = "/id", Type = "jsonpointer" })
                .OnFailureReference("giveUp"))
            .Output("orderId", "$steps.create.outputs.orderId"));

        options.AddWorkflow("trackOrder", workflow => workflow
            .DependsOn("placeOrder")
            .Step("status", step => step
                .Operation("getOrder")
                .Parameter(new ArazzoParameter
                {
                    Name = "id",
                    In = ArazzoParameterLocation.Path,
                    Value = ArazzoValue.FromSelector(new ArazzoSelector
                    {
                        Context = "$workflows.placeOrder.outputs",
                        Selector = "$.orderId",
                        Type = "jsonpath",
                    }),
                })));
    }

    /// <summary>A fresh page on an Arazzo document, by default the field reference, once its workflows have rendered.</summary>
    public async Task<IPage> OpenArazzoDocumentAsync(IList<string>? consoleErrors = null, string documentSlug = "arazzo-field-reference")
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
        await page.GotoAsync($"{BaseAddress}/scalar/#{documentSlug}");
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
