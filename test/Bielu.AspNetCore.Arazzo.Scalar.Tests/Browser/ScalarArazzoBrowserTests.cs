using Microsoft.Playwright;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.Arazzo.Scalar.Tests.Browser;

/// <summary>
/// End-to-end checks of the Arazzo plugin in a real Scalar page: what the unit tests of the bundle cannot
/// see, such as Scalar handing the plugin its documents, rendering, links across documents, and keys.
/// </summary>
public class ScalarArazzoBrowserTests(ScalarArazzoBrowserFixture fixture) : IClassFixture<ScalarArazzoBrowserFixture>
{
    [BrowserFact]
    public async Task ArazzoDocument_RendersWithoutErrors()
    {
        // Arrange
        var errors = new List<string>();

        // Act
        var page = await fixture.OpenArazzoDocumentAsync(errors);

        // Assert
        (await page.Locator(".bielu-arazzo h1").TextContentAsync()).ShouldBe("Arazzo field reference");
        (await page.Locator(".bielu-arazzo .ba-workflow").CountAsync()).ShouldBe(2);
        errors.ShouldBeEmpty();
    }

    [BrowserFact]
    public async Task StepTargets_NameTheirSpec()
    {
        // Arrange
        var page = await fixture.OpenArazzoDocumentAsync();

        // Act
        var badges = await page.Locator(".bielu-arazzo .ba-target .ba-spec").AllTextContentsAsync();

        // Assert
        badges.Select(Normalize).ShouldBe(
        [
            "OpenAPI", // create: operationPath
            "AsyncAPI", // confirmed: channelPath, receive
            "OpenAPI", // read: qualified operationId
            "OpenAPI", // retryCreate: bare operationId
            "AsyncAPI", // announce: AsyncAPI operationId
            "Workflow", // nested
        ]);
        (await page.Locator(".bielu-arazzo .ba-sources .ba-spec").AllTextContentsAsync()).Select(Normalize)
            .ShouldBe(["OpenAPI 3.1.1", "AsyncAPI 3.0.0"]);
    }

    [BrowserFact]
    public async Task FieldReference_ShowsEveryArazzo11Field()
    {
        // Arrange
        var page = await fixture.OpenArazzoDocumentAsync();

        // Act
        var text = Normalize(await page.Locator(".bielu-arazzo").InnerTextAsync());

        // Assert
        text.ShouldSatisfyAllConditions(
            () => text.ShouldContain("Canonical URL /fixtures/field-reference.arazzo.json"),
            () => text.ShouldContain("Replacements"),
            () => text.ShouldContain("jsonpath rfc9535"),
            () => text.ShouldContain("xpath xpath-30"),
            () => text.ShouldContain("Timeout: 15 s"),
            () => text.ShouldContain("Correlation ID: $steps.create.outputs.orderId"),
            () => text.ShouldContain("RECEIVE"),
            () => text.ShouldContain("Reusable components"),
            () => text.ShouldContain("jsonpointer"),
            () => text.ShouldContain("Extensions (1)"));
    }

    [BrowserFact]
    public async Task TargetLink_OpensTheOtherDocument_AndBackReturns()
    {
        // Arrange
        var page = await fixture.OpenArazzoDocumentAsync();
        var link = page.Locator(".bielu-arazzo a.ba-target-link", new() { HasText = "POST /orders" }).First;

        // Act
        await link.ClickAsync();
        await page.WaitForURLAsync(url => new Uri(url).Fragment.StartsWith("#orders/", StringComparison.Ordinal));
        await page.Locator(".bielu-arazzo").WaitForAsync(new() { State = WaitForSelectorState.Detached });
        var operationShown = await page.Locator("h1, h2, h3", new() { HasText = "Create an order" }).First.IsVisibleAsync();
        await page.GoBackAsync();
        await page.Locator(".bielu-arazzo").WaitForAsync();

        // Assert
        operationShown.ShouldBeTrue();
        new Uri(page.Url).Fragment.ShouldStartWith("#arazzo-field-reference");
    }

    [BrowserFact]
    public async Task Filter_NarrowsTheSteps_AndKeepsTheirNumbers()
    {
        // Arrange
        var page = await fixture.OpenArazzoDocumentAsync();

        // Act
        await page.GetByRole(AriaRole.Searchbox, new() { Name = "Filter workflows and steps" }).FillAsync("receive");
        var visibleSteps = page.Locator(".bielu-arazzo .ba-steps > li:visible");

        // Assert
        (await visibleSteps.CountAsync()).ShouldBe(1);
        (await visibleSteps.First.GetAttributeAsync("data-step-number")).ShouldBe("2");
        (await page.Locator(".bielu-arazzo .ba-filter").InnerTextAsync()).ShouldContain("1 of 6 steps");
    }

    [BrowserFact]
    public async Task Overview_CanBeUsedFromTheKeyboard()
    {
        // Arrange
        var page = await fixture.OpenArazzoDocumentAsync();
        var diagram = page.Locator(".bielu-arazzo .ba-diagram").First;

        // Act
        await diagram.FocusAsync();
        await page.Keyboard.PressAsync("+");
        var transform = await page.Locator(".bielu-arazzo .ba-diagram-canvas").First.EvaluateAsync<string>("canvas => canvas.style.transform");
        await page.Keyboard.PressAsync("Tab");
        var focused = await page.EvaluateAsync<string?>("() => document.activeElement?.getAttribute('aria-label')");

        // Assert
        transform.ShouldContain("scale(1.1)");
        focused.ShouldBe("Go to step create (OpenAPI)");
    }

    [BrowserFact]
    public async Task Steps_CollapseTogetherOrOneByOne_AndKeepASummary()
    {
        // Arrange
        var page = await fixture.OpenArazzoDocumentAsync();
        var workflow = page.Locator(".bielu-arazzo .ba-workflow").First;
        var toggles = workflow.Locator(".ba-step-toggle");

        // Act
        await workflow.GetByRole(AriaRole.Button, new() { Name = "Collapse steps" }).ClickAsync();
        var collapsed = await toggles.EvaluateAllAsync<string[]>("buttons => buttons.map(button => button.getAttribute('aria-expanded'))");
        var summary = Normalize(await workflow.Locator(".ba-step-collapsed-summary").First.InnerTextAsync());
        await toggles.First.ClickAsync();
        var afterOne = await toggles.EvaluateAllAsync<string[]>("buttons => buttons.map(button => button.getAttribute('aria-expanded'))");

        // Assert
        collapsed.ShouldAllBe(state => state == "false");
        summary.ShouldBe("OpenAPI Calls operation createOrder POST /orders");
        afterOne.ShouldBe(["true", "false", "false"]);
        (await workflow.Locator(".ba-step-body").First.IsVisibleAsync()).ShouldBeTrue();
    }

    [BrowserFact]
    public async Task GeneratedDocument_ShowsBuilderAuthoredReplacementsSelectorsAndWorkflowOutputs()
    {
        // Arrange
        var errors = new List<string>();
        var page = await fixture.OpenArazzoDocumentAsync(errors, ScalarArazzoBrowserFixture.GeneratedDocumentSlug);

        // Act
        var text = Normalize(await page.Locator(".bielu-arazzo").InnerTextAsync());
        var replacements = (await page.Locator(".bielu-arazzo .ba-replacements tr").AllInnerTextsAsync())
            .Select(Normalize).ToList();

        // Assert
        (await page.Locator(".bielu-arazzo h1").TextContentAsync()).ShouldBe("Generated workflows");
        (await page.Locator(".bielu-arazzo .ba-workflow").CountAsync()).ShouldBe(2);
        replacements.Count.ShouldBe(2);
        replacements[0].ShouldContain("/sku");
        replacements[0].ShouldContain("$inputs.sku");
        replacements[1].ShouldContain("$.quantity");
        replacements[1].ShouldContain("$.lines[0].quantity on $inputs.cart");
        text.ShouldSatisfyAllConditions(
            () => text.ShouldContain("/id on $response.body"),
            () => text.ShouldContain("$.orderId on $workflows.placeOrder.outputs"),
            () => text.ShouldContain("$steps.create.outputs.orderId"),
            () => text.ShouldContain("giveUp"));
        (await page.Locator(".bielu-arazzo .ba-selector").CountAsync()).ShouldBeGreaterThanOrEqualTo(3);
        errors.ShouldBeEmpty();
    }

    [BrowserFact]
    public async Task Overview_DrawsWorkflowCallsRetriesThroughThemAndNamedEnds()
    {
        // Arrange
        var errors = new List<string>();
        var page = await fixture.OpenArazzoDocumentAsync(errors, ScalarArazzoBrowserFixture.AccountDocumentSlug);
        var loadAccount = page.Locator(".bielu-arazzo .ba-workflow", new() { HasText = "Workflow · loadAccount" }).Locator(".ba-overview .ba-diagram");

        // Act
        var workflowNodes = await loadAccount.Locator(".ba-diagram-node-workflow text").AllTextContentsAsync();
        var ends = await loadAccount.Locator(".ba-diagram-node-failure text").AllTextContentsAsync();
        var successes = await loadAccount.Locator(".ba-diagram-node-success").CountAsync();
        var returns = await loadAccount.Locator(".ba-diagram-edge.ba-diagram-return").CountAsync();

        // Assert
        workflowNodes.Select(Normalize).ShouldContain("signIn");
        ends.Select(Normalize).ShouldBe(["forbidden", "error"]);
        successes.ShouldBe(1);
        returns.ShouldBe(1);
        errors.ShouldBeEmpty();
    }

    [BrowserFact]
    public async Task Overview_ExpandsWorkflowNodesIntoGroups_Recursively()
    {
        // Arrange
        var page = await fixture.OpenArazzoDocumentAsync(documentSlug: ScalarArazzoBrowserFixture.AccountDocumentSlug);
        var diagram = page.Locator(".bielu-arazzo .ba-workflow", new() { HasText = "Workflow · browseOrders" }).Locator(".ba-overview .ba-diagram");

        // Act
        await diagram.GetByRole(AriaRole.Button, new() { Name = "Show the steps of workflow loadAccount" }).ClickAsync();
        var afterFirst = await diagram.Locator(".ba-diagram-group").CountAsync();
        var nested = await diagram.Locator("[data-node-id='loadAccount/getAccount']").CountAsync();
        // The signIn that getAccount retries through, inside loadAccount (listOrders has its own, at the top level).
        await diagram.Locator("[data-node-id='loadAccount/__workflow__:signIn']")
            .GetByRole(AriaRole.Button, new() { Name = "Show the steps of workflow signIn" }).FocusAsync();
        await page.Keyboard.PressAsync("Enter");
        var afterSecond = await diagram.Locator(".ba-diagram-group").CountAsync();
        await diagram.GetByRole(AriaRole.Button, new() { Name = "Hide the steps of workflow loadAccount" }).ClickAsync();
        var afterHiding = await diagram.Locator(".ba-diagram-group").CountAsync();

        // Assert
        afterFirst.ShouldBe(1);
        nested.ShouldBe(1);
        afterSecond.ShouldBe(2);
        afterHiding.ShouldBe(0);
    }

    [BrowserFact]
    public async Task FullScreenOverview_StaysOpenAndZoomed_WhenAWorkflowNodeIsCollapsed()
    {
        // Arrange
        var page = await fixture.OpenArazzoDocumentAsync(documentSlug: ScalarArazzoBrowserFixture.AccountDocumentSlug);
        var workflow = page.Locator(".bielu-arazzo .ba-workflow", new() { HasText = "Workflow · browseOrders" });
        await workflow.GetByRole(AriaRole.Button, new() { Name = "Expand workflow diagram" }).ClickAsync();
        var dialog = workflow.Locator("dialog[open]");
        var diagram = dialog.Locator(".ba-diagram");
        await diagram.HoverAsync();
        await page.Mouse.WheelAsync(0, -100);
        var canvas = dialog.Locator(".ba-diagram-canvas");
        var zoomed = await canvas.EvaluateAsync<string>("canvas => canvas.style.transform");

        // Act
        await diagram.GetByRole(AriaRole.Button, new() { Name = "Show the steps of workflow loadAccount" }).ClickAsync();
        await diagram.GetByRole(AriaRole.Button, new() { Name = "Hide the steps of workflow loadAccount" }).ClickAsync();

        // Assert
        zoomed.ShouldContain("scale(1.1)");
        (await dialog.CountAsync()).ShouldBe(1);
        (await canvas.EvaluateAsync<string>("canvas => canvas.style.transform")).ShouldBe(zoomed);
        (await diagram.Locator(".ba-diagram-group").CountAsync()).ShouldBe(0);
    }

    [BrowserTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Overview_KeepsTheToggledNodeInPlaceAndTheZoom_WhenExpandingAndCollapsing(bool fullScreen)
    {
        // Arrange
        var page = await fixture.OpenArazzoDocumentAsync(documentSlug: ScalarArazzoBrowserFixture.AccountDocumentSlug);
        var workflow = page.Locator(".bielu-arazzo .ba-workflow", new() { HasText = "Workflow · loadAccount" });
        if (fullScreen)
        {
            await workflow.GetByRole(AriaRole.Button, new() { Name = "Expand workflow diagram" }).ClickAsync();
        }

        var diagram = workflow.Locator(fullScreen ? "dialog[open] .ba-diagram" : ".ba-overview .ba-diagram");
        await diagram.ScrollIntoViewIfNeededAsync();
        var toggle = diagram.Locator("[data-node-id='__workflow__:signIn'] .ba-diagram-toggle");
        var step = diagram.Locator("[data-node-id='getAccount'] rect").First;
        var before = await toggle.BoundingBoxAsync();
        var stepBefore = await step.BoundingBoxAsync();

        // Act
        await toggle.ClickAsync();
        var expanded = await toggle.BoundingBoxAsync();
        var stepExpanded = await step.BoundingBoxAsync();
        await toggle.ClickAsync();
        var collapsed = await toggle.BoundingBoxAsync();
        var stepCollapsed = await step.BoundingBoxAsync();

        // Assert: the toggle stays under the pointer, and nothing is rescaled.
        foreach (var after in new[] { expanded, collapsed })
        {
            Math.Abs(after!.X - before!.X).ShouldBeLessThan(2);
            Math.Abs(after.Y - before.Y).ShouldBeLessThan(2);
        }

        foreach (var after in new[] { stepExpanded, stepCollapsed })
        {
            Math.Abs(after!.Width - stepBefore!.Width).ShouldBeLessThan(1);
        }
    }

    [BrowserFact]
    public async Task PagesView_ShowsPagesAndPlainLabels_AndKeepsTheZoom()
    {
        // Arrange
        var page = await fixture.OpenArazzoDocumentAsync(documentSlug: ScalarArazzoBrowserFixture.AccountDocumentSlug);
        var workflow = page.Locator(".bielu-arazzo .ba-workflow", new() { HasText = "Workflow · browseOrders" });
        var diagram = workflow.Locator(".ba-overview .ba-diagram");
        await diagram.ScrollIntoViewIfNeededAsync();
        await diagram.HoverAsync();
        await page.Mouse.WheelAsync(0, -100);
        var canvas = workflow.Locator(".ba-overview .ba-diagram-canvas");
        var zoomed = await canvas.EvaluateAsync<string>("canvas => canvas.style.transform");
        var scale = await diagram.Locator("svg").First.GetAttributeAsync("data-scale");

        // Act
        await workflow.Locator(".ba-overview").GetByRole(AriaRole.Button, new() { Name = "Pages" }).ClickAsync();
        var bands = (await diagram.Locator(".ba-diagram-band text").AllTextContentsAsync()).Select(Normalize).ToList();
        var labels = (await diagram.Locator(".ba-diagram-node text").AllTextContentsAsync()).Select(Normalize).ToList();
        var stepPage = Normalize(await workflow.Locator(".ba-step .ba-step-page").Last.InnerTextAsync());

        // Assert
        zoomed.ShouldContain("scale(1.1)");
        (await canvas.EvaluateAsync<string>("canvas => canvas.style.transform")).ShouldBe(zoomed);
        (await diagram.Locator("svg").First.GetAttributeAsync("data-scale")).ShouldBe(scale);
        bands.ShouldContain("Order list · /orders");
        bands.ShouldContain("No page");
        labels.ShouldContain("Load the order list");
        labels.ShouldContain("Load the customer account");
        labels.ShouldContain("No permission");
        stepPage.ShouldBe("Order list · /orders Load the order list");
    }

    [BrowserFact]
    public async Task PagesView_IsNotOfferedForADocumentThatDoesNotUseIt()
    {
        // Arrange / Act
        var page = await fixture.OpenArazzoDocumentAsync(documentSlug: ScalarArazzoBrowserFixture.GeneratedDocumentSlug);
        var toggles = page.Locator(".bielu-arazzo .ba-view-toggle");

        // Assert: every workflow still offers its other views, just not Pages.
        (await toggles.CountAsync()).ShouldBeGreaterThan(0);
        for (var index = 0; index < await toggles.CountAsync(); index++)
        {
            var options = (await toggles.Nth(index).Locator("button").AllInnerTextsAsync()).Select(Normalize).ToList();
            options.ShouldContain("Data flow");
            options.ShouldNotContain("Pages");
        }
    }

    [BrowserFact]
    public async Task WorkflowStep_ShowsTheStepsOfItsWorkflowInline()
    {
        // Arrange
        var page = await fixture.OpenArazzoDocumentAsync(documentSlug: ScalarArazzoBrowserFixture.AccountDocumentSlug);
        var step = page.Locator(".bielu-arazzo .ba-workflow", new() { HasText = "Workflow · browseOrders" }).Locator(".ba-step").First;

        // Act
        await step.GetByRole(AriaRole.Button, new() { Name = "Show its 1 step" }).ClickAsync();
        var nested = Normalize(await step.GetByRole(AriaRole.List, new() { Name = "Steps of workflow loadAccount" }).InnerTextAsync());

        // Assert
        nested.ShouldBe("getAccount calls getOrder");
    }

    private static string Normalize(string? text) => string.Join(' ', (text ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
