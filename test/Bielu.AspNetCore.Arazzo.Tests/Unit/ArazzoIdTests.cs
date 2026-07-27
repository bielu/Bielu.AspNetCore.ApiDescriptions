using Bielu.AspNetCore.Arazzo;
using Bielu.AspNetCore.Arazzo.Services;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.Arazzo.Tests.Unit;

/// <summary>Marker types used by the generic builder overloads.</summary>
internal sealed class MeasureAndAlert;

internal sealed class PublishMeasurement;

internal sealed class AwaitAlert;

internal sealed class HTTPHealthCheck;

public class ArazzoIdTests
{
    [Fact]
    public void FromType_CamelCasesTheTypeName()
    {
        ArazzoId.FromType<MeasureAndAlert>().ShouldBe("measureAndAlert");
    }

    [Fact]
    public void FromType_CamelCasesLeadingAcronyms()
    {
        ArazzoId.FromType<HTTPHealthCheck>().ShouldBe("httpHealthCheck");
    }

    [Fact]
    public void AddWorkflowOfT_UsesTheConventionalWorkflowId()
    {
        var options = new ArazzoOptions();

        options.AddWorkflow<MeasureAndAlert>(wf => wf
            .Step<PublishMeasurement>(s => s.Workflow("other")));

        options.Workflows.ShouldHaveSingleItem().WorkflowId.ShouldBe("measureAndAlert");
    }

    [Fact]
    public void StepOfT_UsesTheConventionalStepId()
    {
        var options = new ArazzoOptions();

        options.AddWorkflow<MeasureAndAlert>(wf => wf
            .Step<PublishMeasurement>(s => s.Workflow("other")));

        options.Workflows[0].Steps.ShouldHaveSingleItem().StepId.ShouldBe("publishMeasurement");
    }

    [Fact]
    public void StepDependsOnOfT_ResolvesToTheSameIdStepOfTProduces()
    {
        var options = new ArazzoOptions();

        options.AddWorkflow<MeasureAndAlert>(wf => wf
            .Step<PublishMeasurement>(s => s.Workflow("other"))
            .Step<AwaitAlert>(s => s
                .DependsOn<PublishMeasurement>()
                .Workflow("other")));

        var awaitAlert = options.Workflows[0].Steps[1];
        awaitAlert.StepId.ShouldBe("awaitAlert");
        awaitAlert.DependsOn.ShouldBe(["publishMeasurement"]);
    }

    [Fact]
    public void WorkflowDependsOnOfT_ChainsIntoASingleList()
    {
        var options = new ArazzoOptions();

        options.AddWorkflow<AwaitAlert>(wf => wf
            .DependsOn<MeasureAndAlert>()
            .DependsOn<PublishMeasurement>()
            .Step("only", s => s.Workflow("other")));

        options.Workflows[0].DependsOn.ShouldBe(["measureAndAlert", "publishMeasurement"]);
    }

    [Fact]
    public void StepWorkflowOfT_TargetsTheConventionalWorkflowId()
    {
        var options = new ArazzoOptions();

        options.AddWorkflow("caller", wf => wf
            .Step("delegate", s => s.Workflow<MeasureAndAlert>()));

        options.Workflows[0].Steps[0].WorkflowId.ShouldBe("measureAndAlert");
    }

    [Fact]
    public void GenericAndStringOverloads_ProduceMatchingIds()
    {
        var options = new ArazzoOptions();

        // The string form a user would naturally write, and the generic form, must agree — otherwise
        // mixing the two silently produces a dangling dependsOn reference.
        options.AddWorkflow("measureAndAlert", wf => wf.Step("only", s => s.Workflow("other")));
        options.AddWorkflow("consumer", wf => wf
            .DependsOn<MeasureAndAlert>()
            .Step("only", s => s.Workflow("other")));

        options.Workflows[1].DependsOn.ShouldBe([options.Workflows[0].WorkflowId]);
    }
}
