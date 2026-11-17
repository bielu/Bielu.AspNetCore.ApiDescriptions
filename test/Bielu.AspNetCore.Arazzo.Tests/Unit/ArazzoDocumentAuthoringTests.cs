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

public class ArazzoDocumentAuthoringTests
{
    private static Task<ArazzoDocument> BuildDocumentAsync(Action<ArazzoOptions> configure,
        ArazzoVersion version = ArazzoVersion.V1_1)
    {
        var services = new ServiceCollection();
        services.AddArazzo(options =>
        {
            options.Version = version;
            options.ValidateSourceReferencesOnStartup = false;
            options.WithInfo("Test", "1.0.0");
            options.AddSourceDescription("api", "https://example.com/openapi.json", ArazzoSourceDescriptionType.OpenApi);
            configure(options);
        });
        return services.BuildServiceProvider()
            .GetRequiredKeyedService<IArazzoDocumentProvider>(ArazzoDefaults.DefaultDocumentName)
            .GetArazzoDocumentAsync();
    }

    private static void AddComponents(ArazzoOptions options) => options
        .AddComponentInputs("orderInputs", JsonNode.Parse("""{"type":"object","properties":{"orderId":{"type":"string"}}}""")!)
        .AddComponentParameter("page", new ArazzoParameter { Name = "page", In = ArazzoParameterLocation.Query, Value = ArazzoValue.FromLiteral(1) })
        .AddComponentSuccessAction("done", new ArazzoSuccessAction { Name = "done", Type = ArazzoSuccessActionType.End })
        .AddComponentFailureAction("retry", new ArazzoFailureAction { Name = "retry", Type = ArazzoFailureActionType.Retry, RetryAfter = 1, RetryLimit = 3, StepId = "get" });

    [Fact]
    public async Task GeneratedDocument_WithSelfComponentsReferencesAndExtensions_ValidatesAndRoundTrips()
    {
        // Arrange
        var document = await BuildDocumentAsync(options =>
        {
            AddComponents(options);
            options.WithSelf("https://example.com/arazzo/orders.json")
                .WithExtension("x-owner", "orders-team")
                .AddWorkflow("getOrder", wf => wf
                    .WithInputsReference("orderInputs")
                    .ParameterReference("page")
                    .OnSuccessReference("done")
                    .OnFailureReference("retry")
                    .WithExtension("x-tier", 1)
                    .Step("get", s => s
                        .Operation("getOrder")
                        .ParameterReference("page", "2")
                        .OnSuccessReference("done")
                        .OnFailureReference("retry")
                        .WithExtension("x-note", null)));
        });

        foreach (var serialized in new[] { ArazzoJsonWriter.Write(document), ArazzoYamlWriter.Write(document) })
        {
            // Act
            var result = ArazzoStringReader.Read(serialized);

            // Assert
            result.Diagnostics.Errors.ShouldBeEmpty();
            var read = result.Document!;
            read.Self.ShouldBe("https://example.com/arazzo/orders.json");
            read.Extensions!["x-owner"]!.GetValue<string>().ShouldBe("orders-team");
            read.Components!.Inputs!.ShouldContainKey("orderInputs");
            read.Components.Parameters!["page"].In.ShouldBe(ArazzoParameterLocation.Query);
            read.Components.SuccessActions!.ShouldContainKey("done");
            read.Components.FailureActions!["retry"].RetryLimit.ShouldBe(3);

            var workflow = read.Workflows[0];
            workflow.Inputs!["$ref"]!.GetValue<string>().ShouldBe("#/components/inputs/orderInputs");
            workflow.Parameters!.ShouldHaveSingleItem().Reference!.Reference.ShouldBe("$components.parameters.page");
            workflow.SuccessActions!.ShouldHaveSingleItem().Reference!.Reference.ShouldBe("$components.successActions.done");
            workflow.FailureActions!.ShouldHaveSingleItem().Reference!.Reference.ShouldBe("$components.failureActions.retry");
            workflow.Extensions!["x-tier"]!.GetValue<int>().ShouldBe(1);

            var step = workflow.Steps[0];
            var parameter = step.Parameters!.ShouldHaveSingleItem().Reference!;
            parameter.Value.ShouldBe("2");
            step.OnSuccess!.ShouldHaveSingleItem().IsReference.ShouldBeTrue();
            step.OnFailure!.ShouldHaveSingleItem().IsReference.ShouldBeTrue();
            step.Extensions!.ShouldContainKey("x-note");
        }
    }

    [Fact]
    public async Task GeneratedDocument_ReferencingMissingComponent_FailsValidation()
    {
        // Act
        var exception = await Should.ThrowAsync<ArazzoDocumentValidationException>(() => BuildDocumentAsync(options => options
            .AddWorkflow("wf", wf => wf
                .WithInputsReference("missingInputs")
                .Step("get", s => s.Operation("getOrder").OnFailureReference("missingAction")))));

        // Assert
        exception.Errors.Select(e => e.Path).ShouldBe(
            ["/workflows/0/inputs/$ref", "/workflows/0/steps/0/onFailure/0/reference"], ignoreOrder: true);
    }

    [Fact]
    public async Task GeneratedDocument_WithInvalidComponentAction_FailsValidation()
    {
        // Act
        var exception = await Should.ThrowAsync<ArazzoDocumentValidationException>(() => BuildDocumentAsync(options => options
            .AddComponentSuccessAction("jump", new ArazzoSuccessAction { Name = "jump", Type = ArazzoSuccessActionType.Goto })
            .AddWorkflow("wf", wf => wf.Step("get", s => s.Operation("getOrder")))));

        // Assert
        exception.Errors.ShouldContain(e => e.Path == "/components/successActions/jump");
    }

    [Fact]
    public async Task GeneratedDocument_WithSelf_FailsValidationWhenDeclaringArazzo10()
    {
        // Act
        var exception = await Should.ThrowAsync<ArazzoDocumentValidationException>(() => BuildDocumentAsync(
            options => options
                .WithSelf("https://example.com/arazzo.json")
                .AddWorkflow("wf", wf => wf.Step("get", s => s.Operation("getOrder"))),
            ArazzoVersion.V1_0));

        // Assert
        exception.Errors.ShouldContain(e => e.Path == "/$self");
    }

    [Fact]
    public async Task GeneratedDocument_WithComponentsInArazzo10_Validates()
    {
        // Act
        var document = await BuildDocumentAsync(
            options =>
            {
                AddComponents(options);
                options.AddWorkflow("wf", wf => wf.Step("get", s => s.Operation("getOrder").ParameterReference("page")));
            },
            ArazzoVersion.V1_0);

        // Assert
        document.Components.ShouldNotBeNull();
    }

    [Theory]
    [InlineData("https://example.com/a.json#frag")]
    [InlineData("")]
    public void WithSelf_RejectsFragmentsAndEmptyValues(string uri)
    {
        Should.Throw<ArgumentException>(() => new ArazzoOptions().WithSelf(uri));
    }

    [Fact]
    public void WithSelf_AcceptsRelativeReferences()
    {
        Should.NotThrow(() => new ArazzoOptions().WithSelf("arazzo/orders.json"));
    }

    [Fact]
    public void AddComponent_RejectsDuplicateAndInvalidNames()
    {
        // Arrange
        var options = new ArazzoOptions();
        AddComponents(options);

        // Act & Assert
        Should.Throw<ArgumentException>(() => options.AddComponentParameter("page", new ArazzoParameter { Name = "page", Value = "$inputs.page" }));
        Should.Throw<ArgumentException>(() => options.AddComponentSuccessAction("bad name", new ArazzoSuccessAction { Name = "x", Type = "end" }));
        Should.Throw<ArgumentException>(() => options.AddComponentInputs("orderInputs", new JsonObject()));
        Should.Throw<ArgumentException>(() => options.AddComponentFailureAction("retry", new ArazzoFailureAction { Name = "x", Type = "end" }));
    }

    [Fact]
    public void AddComponent_RejectsNullValues()
    {
        var options = new ArazzoOptions();
        Should.Throw<ArgumentNullException>(() => options.AddComponentInputs("a", (JsonNode)null!));
        Should.Throw<ArgumentNullException>(() => options.AddComponentInputs("a", (System.Text.Json.Serialization.Metadata.JsonTypeInfo)null!));
        Should.Throw<ArgumentNullException>(() => options.AddComponentParameter("a", null!));
        Should.Throw<ArgumentNullException>(() => options.AddComponentSuccessAction("a", null!));
        Should.Throw<ArgumentNullException>(() => options.AddComponentFailureAction("a", null!));
    }

    [Fact]
    public void AddComponentInputs_FromJsonTypeInfo_AddsTheTypesSchema()
    {
        // Arrange
        var options = new ArazzoOptions();
        var typeInfo = System.Text.Json.JsonSerializerOptions.Default.GetTypeInfo(typeof(OrderInputs));

        // Act
        options.AddComponentInputs("orderInputs", typeInfo);

        // Assert
        options.Components!.Inputs!["orderInputs"]!["properties"]!["OrderId"].ShouldNotBeNull();
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("x-")]
    [InlineData("")]
    public void WithExtension_RejectsNamesWithoutTheXPrefix(string name)
    {
        // Arrange
        var options = new ArazzoOptions();

        // Act & Assert
        Should.Throw<ArgumentException>(() => options.WithExtension(name, 1));
        options.AddWorkflow("wf", wf =>
        {
            Should.Throw<ArgumentException>(() => wf.WithExtension(name, 1));
            wf.Step("s", s =>
            {
                Should.Throw<ArgumentException>(() => s.WithExtension(name, 1));
                s.Operation("op");
            });
        });
    }

    [Fact]
    public void WithExtension_SameNameTwice_KeepsTheLastValue()
    {
        // Arrange & Act
        var options = new ArazzoOptions().WithExtension("x-a", 1).WithExtension("x-a", 2);

        // Assert
        options.Extensions!["x-a"]!.GetValue<int>().ShouldBe(2);
    }

    [Theory]
    [InlineData("")]
    [InlineData("bad name")]
    public void References_RejectInvalidComponentNames(string name)
    {
        new ArazzoOptions().AddWorkflow("wf", wf =>
        {
            Should.Throw<ArgumentException>(() => wf.WithInputsReference(name));
            Should.Throw<ArgumentException>(() => wf.ParameterReference(name));
            Should.Throw<ArgumentException>(() => wf.OnSuccessReference(name));
            Should.Throw<ArgumentException>(() => wf.OnFailureReference(name));
            wf.Step("s", s =>
            {
                Should.Throw<ArgumentException>(() => s.ParameterReference(name));
                Should.Throw<ArgumentException>(() => s.OnSuccessReference(name));
                Should.Throw<ArgumentException>(() => s.OnFailureReference(name));
                s.Operation("op");
            });
        });
    }

    public sealed record OrderInputs(string OrderId);
}
