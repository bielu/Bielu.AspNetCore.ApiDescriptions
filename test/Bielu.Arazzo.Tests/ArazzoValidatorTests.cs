using System.Text.Json.Nodes;
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
    public void Validate_MinimalValidDocument_ReturnsNoErrors()
    {
        ArazzoValidator.Validate(MinimalValidDocument()).ShouldBeEmpty();
    }

    [Fact]
    public void Validate_NullDocument_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => ArazzoValidator.Validate(null!));
    }

    [Fact]
    public void Validate_DuplicateWorkflowIds_ReturnsError()
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
    public void Validate_StepWithNoTarget_ReturnsError()
    {
        var document = MinimalValidDocument();
        document.Workflows[0].Steps[0] = new ArazzoStep { StepId = "orphan" };

        var errors = ArazzoValidator.Validate(document);

        errors.ShouldContain(e => e.Message.Contains("exactly one of operationId"));
    }

    [Fact]
    public void Validate_StepWithMultipleTargets_ReturnsError()
    {
        var document = MinimalValidDocument();
        document.Workflows[0].Steps[0] = new ArazzoStep { StepId = "ambiguous", OperationId = "$sourceDescriptions.a.op", WorkflowId = "otherWf" };

        var errors = ArazzoValidator.Validate(document);

        errors.ShouldContain(e => e.Message.Contains("more than one was set"));
    }

    [Fact]
    public void Validate_UnknownStepAction_ReturnsError()
    {
        var document = MinimalValidDocument();
        document.Workflows[0].Steps[0] = new ArazzoStep { StepId = "s1", ChannelPath = "#/channels/foo", Action = "publish" };

        var errors = ArazzoValidator.Validate(document);

        errors.ShouldContain(e => e.Message.Contains("expected 'send' or 'receive'"));
    }

    [Fact]
    public void Validate_DuplicateSourceDescriptionNames_ReturnsError()
    {
        var document = MinimalValidDocument();
        document.SourceDescriptions.Add(new ArazzoSourceDescription { Name = "a", Url = "https://example.com/b.json" });

        var errors = ArazzoValidator.Validate(document);

        errors.ShouldContain(e => e.Message.Contains("Duplicate source description name"));
    }

    [Fact]
    public void Validate_UnknownSourceDescriptionType_ReturnsError()
    {
        var document = MinimalValidDocument();
        document.SourceDescriptions[0].Type = "graphql";

        var errors = ArazzoValidator.Validate(document);

        errors.ShouldContain(e => e.Message.Contains("Unknown source description type"));
    }

    [Fact]
    public void Validate_WorkflowWithNoSteps_ReturnsWarning()
    {
        var document = MinimalValidDocument();
        document.Workflows[0].Steps.Clear();

        var errors = ArazzoValidator.Validate(document);

        errors.ShouldContain(e => e.Message.Contains("Workflow has no steps") && e.IsWarning);
    }

    [Fact]
    public void Validate_ValidWorkflowInputsSchema_ReturnsNoSchemaError()
    {
        var document = MinimalValidDocument();
        document.Workflows[0].Inputs = JsonNode.Parse("""{"type":"object","properties":{"name":{"type":"string"}}}""");

        var errors = ArazzoValidator.Validate(document);

        errors.ShouldNotContain(e => e.Message.Contains("valid JSON Schema"));
    }

    [Fact]
    public void Validate_InvalidWorkflowInputsSchema_ReturnsError()
    {
        var document = MinimalValidDocument();
        document.Workflows[0].Inputs = JsonNode.Parse("[1,2,3]");

        var errors = ArazzoValidator.Validate(document);

        errors.ShouldContain(e => e.Message.Contains("valid JSON Schema"));
    }

    [Fact]
    public void Validate_DuplicateStepIds_ReturnsError()
    {
        var document = MinimalValidDocument();
        document.Workflows[0].Steps.Add(new ArazzoStep { StepId = "s1", OperationId = "$sourceDescriptions.a.op2" });

        var errors = ArazzoValidator.Validate(document);

        errors.ShouldContain(e => e.Message.Contains("Duplicate stepId"));
    }

    [Fact]
    public void Validate_CorrelationIdOnNonReceiveStep_ReturnsWarning()
    {
        var document = MinimalValidDocument();
        document.Workflows[0].Steps[0] = new ArazzoStep { StepId = "s1", OperationId = "$sourceDescriptions.a.op", CorrelationId = "corr" };

        var errors = ArazzoValidator.Validate(document);

        errors.ShouldContain(e => e.Message.Contains("correlationId only applies") && e.IsWarning);
    }

    [Fact]
    public void Validate_ChannelPathWithoutAction_ReturnsWarning()
    {
        var document = MinimalValidDocument();
        document.Workflows[0].Steps[0] = new ArazzoStep { StepId = "s1", ChannelPath = "{$sourceDescriptions.a.url}#/channels/foo" };

        var errors = ArazzoValidator.Validate(document);

        errors.ShouldContain(e => e.Message.Contains("SHOULD specify 'action'") && e.IsWarning);
    }

    [Fact]
    public void Validate_SuccessActionWithUnknownType_ReturnsError()
    {
        var document = MinimalValidDocument();
        document.Workflows[0].Steps[0].OnSuccess =
        [
            ArazzoReferenceable<ArazzoSuccessAction>.Of(new ArazzoSuccessAction { Name = "a", Type = "bogus" }),
        ];

        var errors = ArazzoValidator.Validate(document);

        errors.ShouldContain(e => e.Message.Contains("Unknown success action type"));
    }

    [Fact]
    public void Validate_GotoSuccessActionWithoutTarget_ReturnsError()
    {
        var document = MinimalValidDocument();
        document.Workflows[0].Steps[0].OnSuccess =
        [
            ArazzoReferenceable<ArazzoSuccessAction>.Of(new ArazzoSuccessAction { Name = "a", Type = ArazzoSuccessActionType.Goto }),
        ];

        var errors = ArazzoValidator.Validate(document);

        errors.ShouldContain(e => e.Message.Contains("requires either workflowId or stepId"));
    }

    [Fact]
    public void Validate_SuccessActionWithBothWorkflowIdAndStepId_ReturnsError()
    {
        var document = MinimalValidDocument();
        document.Workflows[0].Steps[0].OnSuccess =
        [
            ArazzoReferenceable<ArazzoSuccessAction>.Of(new ArazzoSuccessAction { Name = "a", Type = ArazzoSuccessActionType.Goto, WorkflowId = "wf", StepId = "s1" }),
        ];

        var errors = ArazzoValidator.Validate(document);

        errors.ShouldContain(e => e.Message.Contains("mutually exclusive"));
    }

    [Fact]
    public void Validate_FailureActionWithUnknownType_ReturnsError()
    {
        var document = MinimalValidDocument();
        document.Workflows[0].Steps[0].OnFailure =
        [
            ArazzoReferenceable<ArazzoFailureAction>.Of(new ArazzoFailureAction { Name = "a", Type = "bogus" }),
        ];

        var errors = ArazzoValidator.Validate(document);

        errors.ShouldContain(e => e.Message.Contains("Unknown failure action type"));
    }

    [Fact]
    public void Validate_RetryFailureActionWithoutTarget_ReturnsError()
    {
        var document = MinimalValidDocument();
        document.Workflows[0].Steps[0].OnFailure =
        [
            ArazzoReferenceable<ArazzoFailureAction>.Of(new ArazzoFailureAction { Name = "a", Type = ArazzoFailureActionType.Retry }),
        ];

        var errors = ArazzoValidator.Validate(document);

        errors.ShouldContain(e => e.Message.Contains("requires either workflowId or stepId"));
    }

    [Fact]
    public void Validate_RetryAfterOnNonRetryFailureAction_ReturnsError()
    {
        var document = MinimalValidDocument();
        document.Workflows[0].Steps[0].OnFailure =
        [
            ArazzoReferenceable<ArazzoFailureAction>.Of(new ArazzoFailureAction { Name = "a", Type = ArazzoFailureActionType.End, RetryAfter = 5 }),
        ];

        var errors = ArazzoValidator.Validate(document);

        errors.ShouldContain(e => e.Message.Contains("only apply when type is 'retry'"));
    }

    [Fact]
    public void Validate_ParameterWithUnknownLocation_ReturnsError()
    {
        var document = MinimalValidDocument();
        document.Workflows[0].Steps[0].Parameters =
        [
            ArazzoReferenceable<ArazzoParameter>.Of(new ArazzoParameter { Name = "p", In = "body", Value = ArazzoValue.FromLiteral(null) }),
        ];

        var errors = ArazzoValidator.Validate(document);

        errors.ShouldContain(e => e.Message.Contains("Unknown parameter location"));
    }

    [Fact]
    public void Validate_NonSimpleCriterionWithoutContext_ReturnsError()
    {
        var document = MinimalValidDocument();
        document.Workflows[0].Steps[0].SuccessCriteria =
        [
            new ArazzoCriterion { Condition = "$.foo", Type = ArazzoCriterionType.JsonPath },
        ];

        var errors = ArazzoValidator.Validate(document);

        errors.ShouldContain(e => e.Message.Contains("context is required"));
    }
}
