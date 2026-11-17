using System.Text.Json.Nodes;
using Bielu.Arazzo;
using Bielu.Arazzo.Models;
using Bielu.Arazzo.Readers;
using Bielu.Arazzo.Writers;
using Bielu.AspNetCore.Arazzo.Extensions;
using Bielu.AspNetCore.Arazzo.Services;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.Arazzo.Tests.Unit;

public class ArazzoStepValueAuthoringTests
{
    private static ArazzoSelector JsonPathSelector(string context, string selector)
        => new() { Context = context, Selector = selector, Type = "jsonpath" };

    private static ArazzoStep BuildStep(Action<ArazzoStepBuilder> configure)
    {
        var options = new ArazzoOptions();
        options.AddWorkflow("wf", wf => wf.Step("step", s => configure(s.Operation("createOrder"))));
        return options.Workflows[0].Steps[0];
    }

    private static Task<ArazzoDocument> BuildDocumentAsync(Action<ArazzoStepBuilder> configure,
        ArazzoVersion version = ArazzoVersion.V1_1)
    {
        var services = new ServiceCollection();
        services.AddArazzo(options =>
        {
            options.Version = version;
            options.ValidateSourceReferencesOnStartup = false;
            options.WithInfo("Test", "1.0.0");
            options.AddSourceDescription("api", "https://example.com/openapi.json", ArazzoSourceDescriptionType.OpenApi);
            options.AddWorkflow("wf", wf => wf.Step("step", s => configure(s.Operation("createOrder"))));
        });
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredKeyedService<IArazzoDocumentProvider>(ArazzoDefaults.DefaultDocumentName)
            .GetArazzoDocumentAsync();
    }

    [Fact]
    public void Replacement_AddedBeforePayload_IsKeptWhenPayloadIsSet()
    {
        // Arrange & Act
        var step = BuildStep(s => s
            .Replacement("/customer/id", "$inputs.customerId")
            .Payload(JsonNode.Parse("""{"customer":{"id":null}}""")!, "application/json"));

        // Assert
        var body = step.RequestBody.ShouldNotBeNull();
        body.ContentType.ShouldBe("application/json");
        body.Payload!.Literal!["customer"].ShouldNotBeNull();
        var replacement = body.Replacements.ShouldHaveSingleItem();
        replacement.Target.ShouldBe("/customer/id");
        replacement.Value.Expression.ShouldBe("$inputs.customerId");
    }

    [Fact]
    public void Replacement_AddedAfterPayload_ProducesTheSameBodyAsBefore()
    {
        // Arrange
        var payload = JsonNode.Parse("""{"customer":{"id":null}}""")!;

        // Act
        var before = BuildStep(s => s.Replacement("/customer/id", "$inputs.customerId").Payload(payload.DeepClone()));
        var after = BuildStep(s => s.Payload(payload.DeepClone()).Replacement("/customer/id", "$inputs.customerId"));

        // Assert
        ArazzoJsonWriter.Write(Wrap(before)).ShouldBe(ArazzoJsonWriter.Write(Wrap(after)));
    }

    [Fact]
    public void Payload_CalledAgain_ReplacesPayloadButKeepsReplacements()
    {
        // Arrange & Act
        var step = BuildStep(s => s
            .Payload(JsonNode.Parse("""{"a":1}""")!, "application/xml")
            .Replacement("$.a", ArazzoValue.FromLiteral(2), "jsonpath")
            .PayloadExpression("$inputs.body"));

        // Assert
        var body = step.RequestBody.ShouldNotBeNull();
        body.Payload!.Expression.ShouldBe("$inputs.body");
        body.ContentType.ShouldBeNull();
        var replacement = body.Replacements.ShouldHaveSingleItem();
        replacement.Value.Literal!.GetValue<int>().ShouldBe(2);
        replacement.TargetSelectorType!.Type.ShouldBe("jsonpath");
    }

    [Fact]
    public void Replacements_KeepTheOrderTheyWereAddedIn()
    {
        // Arrange & Act
        var step = BuildStep(s => s
            .PayloadExpression("$inputs.body")
            .Replacement("/first", "$inputs.a")
            .Replacement("/second", ArazzoValue.FromSelector(JsonPathSelector("$inputs.items", "$[0]"))));

        // Assert
        step.RequestBody!.Replacements!.Select(r => r.Target).ShouldBe(["/first", "/second"]);
        step.RequestBody.Replacements![1].Value.Selector!.Selector.ShouldBe("$[0]");
    }

    [Fact]
    public void Replacement_WithoutPayload_StillProducesARequestBody()
    {
        // Arrange & Act
        var step = BuildStep(s => s.Replacement("/id", "$inputs.id"));

        // Assert
        step.RequestBody!.Payload.ShouldBeNull();
        step.RequestBody.Replacements.ShouldHaveSingleItem();
    }

    [Fact]
    public void Step_WithoutPayloadOrReplacements_HasNoRequestBody()
    {
        // Arrange & Act
        var step = BuildStep(_ => { });

        // Assert
        step.RequestBody.ShouldBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Replacement_RejectsMissingTarget(string? target)
    {
        BuildStep(s => Should.Throw<ArgumentException>(() => s.Replacement(target!, "$inputs.id")));
    }

    [Fact]
    public void Replacement_RejectsNullValue()
    {
        BuildStep(s => Should.Throw<ArgumentNullException>(() => s.Replacement("/id", null!)));
    }

    [Fact]
    public void PayloadSelector_SetsSelectorPayload()
    {
        // Arrange & Act
        var step = BuildStep(s => s.PayloadSelector(JsonPathSelector("$inputs.cart", "$.order"), "application/json"));

        // Assert
        step.RequestBody!.Payload!.Selector!.Context.ShouldBe("$inputs.cart");
        step.RequestBody.ContentType.ShouldBe("application/json");
    }

    [Fact]
    public void PayloadSelector_RejectsNull()
    {
        BuildStep(s => Should.Throw<ArgumentNullException>(() => s.PayloadSelector(null!)));
    }

    [Fact]
    public void Parameter_WithParameterObject_AcceptsLiteralExpressionAndSelectorValues()
    {
        // Arrange & Act
        var step = BuildStep(s => s
            .Parameter(new ArazzoParameter { Name = "limit", In = ArazzoParameterLocation.Query, Value = ArazzoValue.FromLiteral(10) })
            .Parameter(new ArazzoParameter { Name = "tenant", In = ArazzoParameterLocation.Header, Value = "$inputs.tenant" })
            .Parameter(new ArazzoParameter
            {
                Name = "orderId",
                In = ArazzoParameterLocation.Path,
                Value = ArazzoValue.FromSelector(new ArazzoSelector { Context = "$inputs.orders", Selector = "/0/id", Type = "jsonpointer" }),
            }));

        // Assert
        var parameters = step.Parameters!.Select(p => p.Value!).ToList();
        parameters[0].Value.Literal!.GetValue<int>().ShouldBe(10);
        parameters[1].Value.Expression.ShouldBe("$inputs.tenant");
        parameters[2].Value.Selector!.Type.Type.ShouldBe("jsonpointer");
        parameters[2].In.ShouldBe(ArazzoParameterLocation.Path);
    }

    [Fact]
    public void Parameter_WithParameterObject_RejectsInvalidArguments()
    {
        BuildStep(s =>
        {
            Should.Throw<ArgumentNullException>(() => s.Parameter((ArazzoParameter)null!));
            Should.Throw<ArgumentException>(() => s.Parameter(new ArazzoParameter { Name = "", Value = "$inputs.id" }));
            Should.Throw<ArgumentException>(() => s.Parameter(new ArazzoParameter { Name = "id", Value = null! }));
        });
    }

    [Fact]
    public void Parameter_WithStringExpression_StillBindsToTheExpressionOverload()
    {
        // Arrange & Act
        var step = BuildStep(s => s.Parameter("id", "$inputs.id", ArazzoParameterLocation.Path));

        // Assert
        step.Parameters!.ShouldHaveSingleItem().Value!.Value.Expression.ShouldBe("$inputs.id");
    }

    [Fact]
    public void Output_WithSelector_DeclaresSelectorOutput()
    {
        // Arrange & Act
        var step = BuildStep(s => s
            .Output("id", "$response.body#/id")
            .Output("firstItem", JsonPathSelector("$response.body", "$.items[0]")));

        // Assert
        step.Outputs!["id"].Expression.ShouldBe("$response.body#/id");
        step.Outputs["firstItem"].Selector!.Selector.ShouldBe("$.items[0]");
    }

    [Fact]
    public void Output_WithSelector_RejectsInvalidArguments()
    {
        BuildStep(s =>
        {
            Should.Throw<ArgumentException>(() => s.Output("", JsonPathSelector("$response.body", "$.id")));
            Should.Throw<ArgumentNullException>(() => s.Output("id", (ArazzoSelector)null!));
        });
    }

    [Fact]
    public async Task GeneratedDocument_WithSelectorsAndReplacements_ValidatesAndRoundTripsThroughJsonAndYaml()
    {
        // Arrange
        var document = await BuildDocumentAsync(s => s
            .Payload(JsonNode.Parse("""{"customer":{"id":null},"items":[]}""")!, "application/json")
            .Replacement("/customer/id", "$inputs.customerId")
            .Replacement("$.items", ArazzoValue.FromSelector(JsonPathSelector("$inputs.cart", "$.items")), "jsonpath")
            .Parameter(new ArazzoParameter { Name = "orderId", In = ArazzoParameterLocation.Path, Value = ArazzoValue.FromSelector(JsonPathSelector("$inputs.order", "$.id")) })
            .Output("firstItem", JsonPathSelector("$response.body", "$.items[0]")));

        foreach (var serialized in new[] { ArazzoJsonWriter.Write(document), ArazzoYamlWriter.Write(document) })
        {
            // Act
            var result = ArazzoStringReader.Read(serialized);

            // Assert
            result.Diagnostics.Errors.ShouldBeEmpty();
            var step = result.Document!.Workflows[0].Steps[0];
            var replacements = step.RequestBody!.Replacements!;
            replacements.Count.ShouldBe(2);
            replacements[0].Value.Expression.ShouldBe("$inputs.customerId");
            replacements[1].TargetSelectorType!.Type.ShouldBe("jsonpath");
            replacements[1].Value.Selector!.Context.ShouldBe("$inputs.cart");
            step.Parameters![0].Value!.Value.Selector!.Selector.ShouldBe("$.id");
            step.Outputs!["firstItem"].Selector!.Selector.ShouldBe("$.items[0]");
        }
    }

    [Fact]
    public async Task GeneratedDocument_WithSelectors_FailsValidationWhenDeclaringArazzo10()
    {
        // Act
        var exception = await Should.ThrowAsync<ArazzoDocumentValidationException>(() => BuildDocumentAsync(
            s => s.Output("firstItem", JsonPathSelector("$response.body", "$.items[0]")),
            ArazzoVersion.V1_0));

        // Assert
        exception.Message.ShouldContain("Selector Objects require Arazzo 1.1.0");
    }

    [Fact]
    public async Task GeneratedDocument_WithExpressionReplacements_ValidatesForArazzo10()
    {
        // Act
        var document = await BuildDocumentAsync(
            s => s.PayloadExpression("$inputs.body").Replacement("/id", "$inputs.id"),
            ArazzoVersion.V1_0);

        // Assert
        document.Arazzo.ShouldBe("1.0.0");
    }

    private static ArazzoDocument Wrap(ArazzoStep step) => new()
    {
        Arazzo = "1.1.0",
        Info = new ArazzoInfo { Title = "t", Version = "1.0.0" },
        SourceDescriptions = [],
        Workflows = [new ArazzoWorkflow { WorkflowId = "wf", Steps = [step] }],
    };
}
