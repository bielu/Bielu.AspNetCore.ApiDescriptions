using Bielu.Arazzo.Models;
using Bielu.Arazzo.Validation;
using Shouldly;
using Xunit;

namespace Bielu.Arazzo.Tests;

public class ArazzoValidatorTests
{
    private static ArazzoDocument MinimalValidDocument() => new()
    {
        Arazzo = "1.1.0",
        Info = new ArazzoInfo { Title = "t", Version = "1.0.0" },
        SourceDescriptions = [new ArazzoSourceDescription { Name = "a", Url = "https://example.com/a.json", Type = ArazzoSourceDescriptionType.OpenApi }],
        Workflows =
        [
            new ArazzoWorkflow
            {
                WorkflowId = "wf",
                Steps = [new ArazzoStep { StepId = "s1", OperationId = "$sourceDescriptions.a.op" }],
            },
        ],
    };

    [Fact]
    public void ValidDocumentProducesNoErrors()
    {
        ArazzoValidator.Validate(MinimalValidDocument()).ShouldBeEmpty();
    }

    [Fact]
    public void FlagsDuplicateWorkflowIds()
    {
        var document = MinimalValidDocument();
        document.Workflows.Add(new ArazzoWorkflow
        {
            WorkflowId = "wf",
            Steps = [new ArazzoStep { StepId = "s2", OperationId = "$sourceDescriptions.a.op2" }],
        });

        var errors = ArazzoValidator.Validate(document);

        errors.ShouldContain(e => e.Message.Contains("Duplicate workflowId"));
    }

    [Fact]
    public void FlagsStepWithNoTarget()
    {
        var document = MinimalValidDocument();
        document.Workflows[0].Steps[0] = new ArazzoStep { StepId = "orphan" };

        var errors = ArazzoValidator.Validate(document);

        errors.ShouldContain(e => e.Message.Contains("exactly one of operationId"));
    }

    [Fact]
    public void FlagsStepWithMultipleTargets()
    {
        var document = MinimalValidDocument();
        document.Workflows[0].Steps[0] = new ArazzoStep { StepId = "ambiguous", OperationId = "$sourceDescriptions.a.op", WorkflowId = "otherWf" };

        var errors = ArazzoValidator.Validate(document);

        errors.ShouldContain(e => e.Message.Contains("more than one was set"));
    }

    [Fact]
    public void FlagsUnknownStepAction()
    {
        var document = MinimalValidDocument();
        document.Workflows[0].Steps[0] = new ArazzoStep { StepId = "s1", ChannelPath = "#/channels/foo", Action = "publish" };

        var errors = ArazzoValidator.Validate(document);

        errors.ShouldContain(e => e.Message.Contains("expected 'send' or 'receive'"));
    }
}
