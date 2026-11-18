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

public class ArazzoWorkflowAuthoringTests
{
    private static ArazzoWorkflow BuildWorkflow(Action<ArazzoWorkflowBuilder> configure)
    {
        var options = new ArazzoOptions();
        options.AddWorkflow("wf", wf =>
        {
            configure(wf);
            wf.Step("step", s => s.Operation("createOrder"));
        });
        return options.Workflows[0];
    }

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

    [Fact]
    public void Output_DeclaresExpressionAndSelectorOutputs()
    {
        // Arrange & Act
        var workflow = BuildWorkflow(wf => wf
            .Output("orderId", "$steps.step.outputs.orderId")
            .Output("first.item", new ArazzoSelector { Context = "$steps.step.outputs.items", Selector = "$[0]", Type = "jsonpath" }));

        // Assert
        workflow.Outputs!["orderId"].Expression.ShouldBe("$steps.step.outputs.orderId");
        workflow.Outputs["first.item"].Selector!.Selector.ShouldBe("$[0]");
    }

    [Fact]
    public void Output_SameNameTwice_KeepsTheLastValue()
    {
        // Arrange & Act
        var workflow = BuildWorkflow(wf => wf.Output("id", "$inputs.a").Output("id", "$inputs.b"));

        // Assert
        workflow.Outputs!.ShouldHaveSingleItem().Value.Expression.ShouldBe("$inputs.b");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("has space")]
    [InlineData("a/b")]
    public void Output_RejectsInvalidNames(string? name)
    {
        BuildWorkflow(wf =>
        {
            Should.Throw<ArgumentException>(() => wf.Output(name!, "$inputs.a"));
            Should.Throw<ArgumentException>(() => wf.Output(name!, new ArazzoSelector { Context = "$inputs", Selector = "$", Type = "jsonpath" }));
        });
    }

    [Fact]
    public void Output_RejectsMissingValues()
    {
        BuildWorkflow(wf =>
        {
            Should.Throw<ArgumentException>(() => wf.Output("id", ""));
            Should.Throw<ArgumentNullException>(() => wf.Output("id", (ArazzoSelector)null!));
        });
    }

    [Fact]
    public void Parameter_AddsWorkflowWideParameters()
    {
        // Arrange & Act
        var workflow = BuildWorkflow(wf => wf
            .Parameter("tenant", "$inputs.tenant", ArazzoParameterLocation.Header)
            .Parameter(new ArazzoParameter { Name = "page", In = ArazzoParameterLocation.Query, Value = ArazzoValue.FromLiteral(1) }));

        // Assert
        var parameters = workflow.Parameters!.Select(p => p.Value!).ToList();
        parameters[0].Value.Expression.ShouldBe("$inputs.tenant");
        parameters[0].In.ShouldBe(ArazzoParameterLocation.Header);
        parameters[1].Value.Literal!.GetValue<int>().ShouldBe(1);
    }

    [Fact]
    public void Parameter_RejectsInvalidArguments()
    {
        BuildWorkflow(wf =>
        {
            Should.Throw<ArgumentException>(() => wf.Parameter("", "$inputs.a"));
            Should.Throw<ArgumentException>(() => wf.Parameter("a", ""));
            Should.Throw<ArgumentNullException>(() => wf.Parameter((ArazzoParameter)null!));
            Should.Throw<ArgumentException>(() => wf.Parameter(new ArazzoParameter { Name = "a", Value = null! }));
        });
    }

    [Fact]
    public void OnSuccessAndOnFailure_AddWorkflowWideActionsInOrder()
    {
        // Arrange & Act
        var workflow = BuildWorkflow(wf => wf
            .OnSuccess(new ArazzoSuccessAction { Name = "done", Type = ArazzoSuccessActionType.End })
            .OnFailure(new ArazzoFailureAction { Name = "retry", Type = ArazzoFailureActionType.Retry, StepId = "step", RetryLimit = 3 })
            .OnFailure(new ArazzoFailureAction { Name = "stop", Type = ArazzoFailureActionType.End }));

        // Assert
        workflow.SuccessActions!.ShouldHaveSingleItem().Value!.Name.ShouldBe("done");
        workflow.FailureActions!.Select(a => a.Value!.Name).ShouldBe(["retry", "stop"]);
    }

    [Fact]
    public void OnSuccessAndOnFailure_RejectNull()
    {
        BuildWorkflow(wf =>
        {
            Should.Throw<ArgumentNullException>(() => wf.OnSuccess(null!));
            Should.Throw<ArgumentNullException>(() => wf.OnFailure(null!));
        });
    }

    [Fact]
    public void Workflow_WithoutNewFields_LeavesThemUnset()
    {
        // Arrange & Act
        var workflow = BuildWorkflow(_ => { });

        // Assert
        workflow.Outputs.ShouldBeNull();
        workflow.Parameters.ShouldBeNull();
        workflow.SuccessActions.ShouldBeNull();
        workflow.FailureActions.ShouldBeNull();
    }

    [Fact]
    public async Task GeneratedDocument_LaterWorkflowConsumesEarlierWorkflowOutputs_ValidatesAndRoundTrips()
    {
        // Arrange
        var document = await BuildDocumentAsync(options => options
            .AddWorkflow("placeOrder", wf => wf
                .Parameter("tenant", "$inputs.tenant", ArazzoParameterLocation.Header)
                .Step("create", s => s.Operation("createOrder").Output("orderId", "$response.body#/id"))
                .Output("orderId", "$steps.create.outputs.orderId")
                .OnFailure(new ArazzoFailureAction { Name = "retry", Type = ArazzoFailureActionType.Retry, StepId = "create", RetryAfter = 1, RetryLimit = 2 }))
            .AddWorkflow("trackOrder", wf => wf
                .DependsOn("placeOrder")
                .Step("track", s => s
                    .Operation("getOrder")
                    .Parameter("orderId", "$workflows.placeOrder.outputs.orderId", ArazzoParameterLocation.Path))
                .OnSuccess(new ArazzoSuccessAction { Name = "done", Type = ArazzoSuccessActionType.End })));

        foreach (var serialized in new[] { ArazzoJsonWriter.Write(document), ArazzoYamlWriter.Write(document) })
        {
            // Act
            var result = ArazzoStringReader.Read(serialized);

            // Assert
            result.Diagnostics.Errors.ShouldBeEmpty();
            var placeOrder = result.Document!.Workflows[0];
            placeOrder.Outputs!["orderId"].Expression.ShouldBe("$steps.create.outputs.orderId");
            placeOrder.Parameters!.ShouldHaveSingleItem().Value!.In.ShouldBe(ArazzoParameterLocation.Header);
            placeOrder.FailureActions!.ShouldHaveSingleItem().Value!.RetryLimit.ShouldBe(2);
            var trackOrder = result.Document.Workflows[1];
            trackOrder.Steps[0].Parameters![0].Value!.Value.Expression.ShouldBe("$workflows.placeOrder.outputs.orderId");
            trackOrder.SuccessActions!.ShouldHaveSingleItem().Value!.Type.ShouldBe(ArazzoSuccessActionType.End);
        }
    }

    [Fact]
    public async Task GeneratedDocument_WithTargetlessRetryFailureAction_Validates()
    {
        // Act
        var document = await BuildDocumentAsync(options => options
            .AddWorkflow("wf", wf => wf
                .Step("step", s => s
                    .Operation("createOrder")
                    .OnFailure(new ArazzoFailureAction { Name = "retryStep", Type = ArazzoFailureActionType.Retry, RetryAfter = 1, RetryLimit = 5 }))));

        // Assert
        var action = document.Workflows[0].Steps[0].OnFailure!.ShouldHaveSingleItem().Value!;
        action.StepId.ShouldBeNull();
        action.WorkflowId.ShouldBeNull();
    }

    [Fact]
    public async Task GeneratedDocument_WithInvalidWorkflowFailureAction_FailsValidation()
    {
        // Act
        var exception = await Should.ThrowAsync<ArazzoDocumentValidationException>(() => BuildDocumentAsync(options => options
            .AddWorkflow("wf", wf => wf
                .Step("step", s => s.Operation("createOrder"))
                .OnFailure(new ArazzoFailureAction { Name = "jump", Type = ArazzoFailureActionType.Goto }))));

        // Assert
        exception.Errors.ShouldContain(e => e.Path == "/workflows/0/failureActions/0");
    }

    [Fact]
    public async Task GeneratedDocument_WithSelectorWorkflowOutput_FailsValidationWhenDeclaringArazzo10()
    {
        // Act
        var exception = await Should.ThrowAsync<ArazzoDocumentValidationException>(() => BuildDocumentAsync(
            options => options.AddWorkflow("wf", wf => wf
                .Step("step", s => s.Operation("createOrder"))
                .Output("id", new ArazzoSelector { Context = "$steps.step.outputs.body", Selector = "$.id", Type = "jsonpath" })),
            ArazzoVersion.V1_0));

        // Assert
        exception.Errors.ShouldContain(e => e.Path == "/workflows/0/outputs/id");
    }
}
