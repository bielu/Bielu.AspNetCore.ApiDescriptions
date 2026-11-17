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
            () => text.ShouldContain("Canonical URL https://example.test/fixtures/field-reference.arazzo.json"),
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

    private static string Normalize(string? text) => string.Join(' ', (text ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
