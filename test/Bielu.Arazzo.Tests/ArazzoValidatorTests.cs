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
        // Arrange
        var document = MinimalValidDocument();

        // Act
        var errors = ArazzoValidator.Validate(document);

        // Assert
        errors.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_NullDocument_ThrowsArgumentNullException()
    {
        // Arrange
        var action = () => { _ = ArazzoValidator.Validate(null!); };

        // Act
        var exception = Record.Exception(action);

        // Assert
        exception.ShouldBeOfType<ArgumentNullException>();
    }

    [Fact]
    public void Validate_DuplicateWorkflowIds_ReturnsError()
    {
        // Arrange
        var document = MinimalValidDocument();
        document.Workflows.Add(new ArazzoWorkflow
        {
            WorkflowId = "wf",
            Steps = [new ArazzoStep { StepId = "s2", OperationId = "$sourceDescriptions.a.op2" }],
        });

        // Act
        var errors = ArazzoValidator.Validate(document);

        // Assert
        errors.ShouldContain(e => e.Message.Contains("Duplicate workflowId"));
    }

    [Fact]
    public void Validate_StepWithNoTarget_ReturnsError()
    {
        // Arrange
        var document = MinimalValidDocument();
        document.Workflows[0].Steps[0] = new ArazzoStep { StepId = "orphan" };

        // Act
        var errors = ArazzoValidator.Validate(document);

        // Assert
        errors.ShouldContain(e => e.Message.Contains("exactly one of operationId"));
    }

    [Fact]
    public void Validate_StepWithMultipleTargets_ReturnsError()
    {
        // Arrange
        var document = MinimalValidDocument();
        document.Workflows[0].Steps[0] = new ArazzoStep { StepId = "ambiguous", OperationId = "$sourceDescriptions.a.op", WorkflowId = "otherWf" };

        // Act
        var errors = ArazzoValidator.Validate(document);

        // Assert
        errors.ShouldContain(e => e.Message.Contains("more than one was set"));
    }

    [Fact]
    public void Validate_UnknownStepAction_ReturnsError()
    {
        // Arrange
        var document = MinimalValidDocument();
        document.Workflows[0].Steps[0] = new ArazzoStep { StepId = "s1", ChannelPath = "#/channels/foo", Action = "publish" };

        // Act
        var errors = ArazzoValidator.Validate(document);

        // Assert
        errors.ShouldContain(e => e.Message.Contains("expected 'send' or 'receive'"));
    }

    [Fact]
    public void Validate_DuplicateSourceDescriptionNames_ReturnsError()
    {
        // Arrange
        var document = MinimalValidDocument();
        document.SourceDescriptions.Add(new ArazzoSourceDescription { Name = "a", Url = "https://example.com/b.json" });

        // Act
        var errors = ArazzoValidator.Validate(document);

        // Assert
        errors.ShouldContain(e => e.Message.Contains("Duplicate source description name"));
    }

    [Fact]
    public void Validate_UnknownSourceDescriptionType_ReturnsError()
    {
        // Arrange
        var document = MinimalValidDocument();
        document.SourceDescriptions[0].Type = "graphql";

        // Act
        var errors = ArazzoValidator.Validate(document);

        // Assert
        errors.ShouldContain(e => e.Message.Contains("Unknown source description type"));
    }

    [Fact]
    public void Validate_WorkflowWithNoSteps_ReturnsWarning()
    {
        // Arrange
        var document = MinimalValidDocument();
        document.Workflows[0].Steps.Clear();

        // Act
        var errors = ArazzoValidator.Validate(document);

        // Assert
        errors.ShouldContain(e => e.Message.Contains("Workflow has no steps") && e.IsWarning);
    }

    [Fact]
    public void Validate_ValidWorkflowInputsSchema_ReturnsNoSchemaError()
    {
        // Arrange
        var document = MinimalValidDocument();
        document.Workflows[0].Inputs = JsonNode.Parse("""{"type":"object","properties":{"name":{"type":"string"}}}""");

        // Act
        var errors = ArazzoValidator.Validate(document);

        // Assert
        errors.ShouldNotContain(e => e.Message.Contains("valid JSON Schema"));
    }

    [Fact]
    public void Validate_InvalidWorkflowInputsSchema_ReturnsError()
    {
        // Arrange
        var document = MinimalValidDocument();
        document.Workflows[0].Inputs = JsonNode.Parse("[1,2,3]");

        // Act
        var errors = ArazzoValidator.Validate(document);

        // Assert
        errors.ShouldContain(e => e.Message.Contains("valid JSON Schema"));
    }

    [Fact]
    public void Validate_DuplicateStepIds_ReturnsError()
    {
        // Arrange
        var document = MinimalValidDocument();
        document.Workflows[0].Steps.Add(new ArazzoStep { StepId = "s1", OperationId = "$sourceDescriptions.a.op2" });

        // Act
        var errors = ArazzoValidator.Validate(document);

        // Assert
        errors.ShouldContain(e => e.Message.Contains("Duplicate stepId"));
    }

    [Fact]
    public void Validate_CorrelationIdOnNonReceiveStep_ReturnsWarning()
    {
        // Arrange
        var document = MinimalValidDocument();
        document.Workflows[0].Steps[0] = new ArazzoStep { StepId = "s1", OperationId = "$sourceDescriptions.a.op", CorrelationId = "corr" };

        // Act
        var errors = ArazzoValidator.Validate(document);

        // Assert
        errors.ShouldContain(e => e.Message.Contains("correlationId only applies") && e.IsWarning);
    }

    [Fact]
    public void Validate_ChannelPathWithoutAction_ReturnsWarning()
    {
        // Arrange
        var document = MinimalValidDocument();
        document.Workflows[0].Steps[0] = new ArazzoStep { StepId = "s1", ChannelPath = "{$sourceDescriptions.a.url}#/channels/foo" };

        // Act
        var errors = ArazzoValidator.Validate(document);

        // Assert
        errors.ShouldContain(e => e.Message.Contains("SHOULD specify 'action'") && e.IsWarning);
    }

    [Fact]
    public void Validate_SuccessActionWithUnknownType_ReturnsError()
    {
        // Arrange
        var document = MinimalValidDocument();
        document.Workflows[0].Steps[0].OnSuccess =
        [
            ArazzoReferenceable<ArazzoSuccessAction>.Of(new ArazzoSuccessAction { Name = "a", Type = "bogus" }),
        ];

        // Act
        var errors = ArazzoValidator.Validate(document);

        // Assert
        errors.ShouldContain(e => e.Message.Contains("Unknown success action type"));
    }

    [Fact]
    public void Validate_GotoSuccessActionWithoutTarget_ReturnsError()
    {
        // Arrange
        var document = MinimalValidDocument();
        document.Workflows[0].Steps[0].OnSuccess =
        [
            ArazzoReferenceable<ArazzoSuccessAction>.Of(new ArazzoSuccessAction { Name = "a", Type = ArazzoSuccessActionType.Goto }),
        ];

        // Act
        var errors = ArazzoValidator.Validate(document);

        // Assert
        errors.ShouldContain(e => e.Message.Contains("requires either workflowId or stepId"));
    }

    [Fact]
    public void Validate_SuccessActionWithBothWorkflowIdAndStepId_ReturnsError()
    {
        // Arrange
        var document = MinimalValidDocument();
        document.Workflows[0].Steps[0].OnSuccess =
        [
            ArazzoReferenceable<ArazzoSuccessAction>.Of(new ArazzoSuccessAction { Name = "a", Type = ArazzoSuccessActionType.Goto, WorkflowId = "wf", StepId = "s1" }),
        ];

        // Act
        var errors = ArazzoValidator.Validate(document);

        // Assert
        errors.ShouldContain(e => e.Message.Contains("mutually exclusive"));
    }

    [Fact]
    public void Validate_FailureActionWithUnknownType_ReturnsError()
    {
        // Arrange
        var document = MinimalValidDocument();
        document.Workflows[0].Steps[0].OnFailure =
        [
            ArazzoReferenceable<ArazzoFailureAction>.Of(new ArazzoFailureAction { Name = "a", Type = "bogus" }),
        ];

        // Act
        var errors = ArazzoValidator.Validate(document);

        // Assert
        errors.ShouldContain(e => e.Message.Contains("Unknown failure action type"));
    }

    [Fact]
    public void Validate_RetryFailureActionWithoutTarget_ReturnsError()
    {
        // Arrange
        var document = MinimalValidDocument();
        document.Workflows[0].Steps[0].OnFailure =
        [
            ArazzoReferenceable<ArazzoFailureAction>.Of(new ArazzoFailureAction { Name = "a", Type = ArazzoFailureActionType.Retry }),
        ];

        // Act
        var errors = ArazzoValidator.Validate(document);

        // Assert
        errors.ShouldContain(e => e.Message.Contains("requires either workflowId or stepId"));
    }

    [Fact]
    public void Validate_RetryAfterOnNonRetryFailureAction_ReturnsError()
    {
        // Arrange
        var document = MinimalValidDocument();
        document.Workflows[0].Steps[0].OnFailure =
        [
            ArazzoReferenceable<ArazzoFailureAction>.Of(new ArazzoFailureAction { Name = "a", Type = ArazzoFailureActionType.End, RetryAfter = 5 }),
        ];

        // Act
        var errors = ArazzoValidator.Validate(document);

        // Assert
        errors.ShouldContain(e => e.Message.Contains("only apply when type is 'retry'"));
    }

    [Fact]
    public void Validate_ParameterWithUnknownLocation_ReturnsError()
    {
        // Arrange
        var document = MinimalValidDocument();
        document.Workflows[0].Steps[0].Parameters =
        [
            ArazzoReferenceable<ArazzoParameter>.Of(new ArazzoParameter { Name = "p", In = "body", Value = ArazzoValue.FromLiteral(null) }),
        ];

        // Act
        var errors = ArazzoValidator.Validate(document);

        // Assert
        errors.ShouldContain(e => e.Message.Contains("Unknown parameter location"));
    }

    [Fact]
    public void Validate_NonSimpleCriterionWithoutContext_ReturnsError()
    {
        // Arrange
        var document = MinimalValidDocument();
        document.Workflows[0].Steps[0].SuccessCriteria =
        [
            new ArazzoCriterion { Condition = "$.foo", Type = ArazzoCriterionType.JsonPath },
        ];

        // Act
        var errors = ArazzoValidator.Validate(document);

        // Assert
        errors.ShouldContain(e => e.Message.Contains("context is required"));
    }

    [Fact]
    public void Validate_ReferenceToWrongComponentKind_ReturnsError()
    {
        // Arrange
        var document = MinimalValidDocument();
        document.Components = new ArazzoComponents
        {
            SuccessActions = new Dictionary<string, ArazzoSuccessAction> { ["done"] = new() { Name = "done", Type = "end" } },
        };
        document.Workflows[0].Steps[0].OnFailure =
        [
            ArazzoReferenceable<ArazzoFailureAction>.Of(new ArazzoReusableObject { Reference = "$components.successActions.done" }),
        ];

        // Act
        var errors = ArazzoValidator.Validate(document);

        // Assert
        errors.ShouldContain(e => e.Path == "/workflows/0/steps/0/onFailure/0/reference" && e.Message.Contains("components.failureActions"));
    }

    [Fact]
    public void Validate_ValueOnNonParameterReference_ReturnsError()
    {
        // Arrange
        var document = MinimalValidDocument();
        document.Components = new ArazzoComponents
        {
            SuccessActions = new Dictionary<string, ArazzoSuccessAction> { ["done"] = new() { Name = "done", Type = "end" } },
        };
        document.Workflows[0].SuccessActions =
        [
            ArazzoReferenceable<ArazzoSuccessAction>.Of(new ArazzoReusableObject { Reference = "$components.successActions.done", Value = "x" }),
        ];

        // Act
        var errors = ArazzoValidator.Validate(document);

        // Assert
        errors.ShouldHaveSingleItem().Path.ShouldBe("/workflows/0/successActions/0/value");
    }

    [Fact]
    public void Validate_ResolvedReferencesAndInputsRef_ReturnNoErrors()
    {
        // Arrange
        var document = MinimalValidDocument();
        document.Components = new ArazzoComponents
        {
            Inputs = new Dictionary<string, JsonNode?> { ["in"] = JsonNode.Parse("""{"type":"object"}""") },
            Parameters = new Dictionary<string, ArazzoParameter> { ["p"] = new() { Name = "p", In = "query", Value = "$inputs.p" } },
        };
        document.Workflows[0].Inputs = JsonNode.Parse("""{"$ref":"#/components/inputs/in"}""");
        document.Workflows[0].Steps[0].Parameters =
        [
            ArazzoReferenceable<ArazzoParameter>.Of(new ArazzoReusableObject { Reference = "$components.parameters.p", Value = "1" }),
        ];

        // Act
        var errors = ArazzoValidator.Validate(document);

        // Assert
        errors.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_SelfWithFragment_ReturnsError()
    {
        // Arrange
        var document = MinimalValidDocument();
        document.Self = "https://example.com/a.json#x";

        // Act
        var errors = ArazzoValidator.Validate(document);

        // Assert
        errors.ShouldHaveSingleItem().Message.ShouldContain("fragment");
    }

    private static ArazzoSelector Selector() => new() { Context = "$response.body", Selector = "$.id", Type = "jsonpath" };

    [Fact]
    public void Validate_LiteralStepOutput_ReturnsError()
    {
        // Arrange
        var document = MinimalValidDocument();
        document.Workflows[0].Steps[0].Outputs = new Dictionary<string, ArazzoValue>
        {
            ["id"] = ArazzoValue.FromLiteral(JsonValue.Create(42)),
        };

        // Act
        var errors = ArazzoValidator.Validate(document);

        // Assert
        errors.ShouldContain(e => e.Path == "/workflows/0/steps/0/outputs/id" && e.Message.Contains("not a literal"));
    }

    [Fact]
    public void Validate_LiteralWorkflowOutput_ReturnsError()
    {
        // Arrange
        var document = MinimalValidDocument();
        document.Workflows[0].Outputs = new Dictionary<string, ArazzoValue>
        {
            ["a/b"] = ArazzoValue.FromLiteral(JsonValue.Create("x")),
        };

        // Act
        var errors = ArazzoValidator.Validate(document);

        // Assert
        errors.ShouldContain(e => e.Path == "/workflows/0/outputs/a~1b");
    }

    [Fact]
    public void Validate_SelectorValuesInArazzo11Document_ReturnsNoErrors()
    {
        // Arrange
        var document = MinimalValidDocument();
        var step = document.Workflows[0].Steps[0];
        step.Outputs = new Dictionary<string, ArazzoValue> { ["id"] = ArazzoValue.FromSelector(Selector()) };
        step.Parameters = [ArazzoReferenceable<ArazzoParameter>.Of(new ArazzoParameter { Name = "id", In = "path", Value = ArazzoValue.FromSelector(Selector()) })];
        step.RequestBody = new ArazzoRequestBody
        {
            Payload = ArazzoValue.FromSelector(Selector()),
            Replacements = [new ArazzoPayloadReplacement { Target = "$.id", TargetSelectorType = "jsonpath", Value = ArazzoValue.FromSelector(Selector()) }],
        };

        // Act
        var errors = ArazzoValidator.Validate(document);

        // Assert
        errors.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_SelectorValuesInArazzo10Document_ReturnsAnErrorPerPosition()
    {
        // Arrange
        var document = MinimalValidDocument();
        document.Arazzo = "1.0.1";
        var workflow = document.Workflows[0];
        var step = workflow.Steps[0];
        workflow.Outputs = new Dictionary<string, ArazzoValue> { ["id"] = ArazzoValue.FromSelector(Selector()) };
        workflow.Parameters = [ArazzoReferenceable<ArazzoParameter>.Of(new ArazzoParameter { Name = "id", In = "path", Value = ArazzoValue.FromSelector(Selector()) })];
        step.OnFailure =
        [
            ArazzoReferenceable<ArazzoFailureAction>.Of(new ArazzoFailureAction
            {
                Name = "retry",
                Type = ArazzoFailureActionType.Goto,
                WorkflowId = "wf",
                Parameters = [ArazzoReferenceable<ArazzoParameter>.Of(new ArazzoParameter { Name = "id", Value = ArazzoValue.FromSelector(Selector()) })],
            }),
        ];
        step.RequestBody = new ArazzoRequestBody
        {
            Payload = ArazzoValue.FromSelector(Selector()),
            Replacements = [new ArazzoPayloadReplacement { Target = "$.id", TargetSelectorType = "jsonpath", Value = ArazzoValue.FromSelector(Selector()) }],
        };
        document.Components = new ArazzoComponents
        {
            Parameters = new Dictionary<string, ArazzoParameter>
            {
                ["shared"] = new() { Name = "id", In = "query", Value = ArazzoValue.FromSelector(Selector()) },
            },
        };

        // Act
        var errors = ArazzoValidator.Validate(document);

        // Assert
        errors.Select(e => e.Path).ShouldBe(
        [
            "/components/parameters/shared/value",
            "/workflows/0/parameters/0/value",
            "/workflows/0/outputs/id",
            "/workflows/0/steps/0/onFailure/0/parameters/0/value",
            "/workflows/0/steps/0/requestBody/payload",
            "/workflows/0/steps/0/requestBody/replacements/0/targetSelectorType",
            "/workflows/0/steps/0/requestBody/replacements/0/value",
        ], ignoreOrder: true);
    }

    [Fact]
    public void Validate_ExpressionAndLiteralValuesInArazzo10Document_ReturnsNoErrors()
    {
        // Arrange
        var document = MinimalValidDocument();
        document.Arazzo = "1.0.0";
        var step = document.Workflows[0].Steps[0];
        step.Outputs = new Dictionary<string, ArazzoValue> { ["id"] = "$response.body#/id" };
        step.RequestBody = new ArazzoRequestBody
        {
            Payload = ArazzoValue.FromLiteral(JsonNode.Parse("""{"id":null}""")),
            Replacements = [new ArazzoPayloadReplacement { Target = "/id", Value = "$inputs.id" }],
        };

        // Act
        var errors = ArazzoValidator.Validate(document);

        // Assert
        errors.ShouldBeEmpty();
    }

    private static ArazzoDocument DocumentUsingEveryArazzo11StepFeature(string arazzo)
    {
        var document = MinimalValidDocument();
        document.Arazzo = arazzo;
        document.SourceDescriptions.Add(new ArazzoSourceDescription { Name = "events", Url = "https://example.com/asyncapi.json", Type = ArazzoSourceDescriptionType.AsyncApi });
        document.Workflows[0].Steps.Add(new ArazzoStep
        {
            StepId = "s2",
            ChannelPath = "{$sourceDescriptions.events.url}#/channels/orders",
            Action = ArazzoStepAction.Receive,
            CorrelationId = "orderId",
            Timeout = 1000,
            DependsOn = ["s1"],
            Parameters = [ArazzoReferenceable<ArazzoParameter>.Of(new ArazzoParameter { Name = "q", In = ArazzoParameterLocation.QueryString, Value = "$inputs.q" })],
            SuccessCriteria =
            [
                new ArazzoCriterion { Context = "$message.payload", Condition = "/id", Type = "jsonpointer" },
                new ArazzoCriterion { Context = "$message.payload", Condition = "$.id", Type = new ArazzoSelectorType { Type = "jsonpath", Version = "rfc9535" } },
            ],
        });
        return document;
    }

    [Fact]
    public void Validate_Arazzo11StepFeaturesInArazzo10Document_ReturnsAnErrorPerFeature()
    {
        // Arrange
        var document = DocumentUsingEveryArazzo11StepFeature("1.0.1");

        // Act
        var errors = ArazzoValidator.Validate(document);

        // Assert
        errors.Where(e => e.Message.Contains("requires Arazzo 1.1.0")).Select(e => e.Path).ShouldBe(
        [
            "/sourceDescriptions/1/type",
            "/workflows/0/steps/1/channelPath",
            "/workflows/0/steps/1/action",
            "/workflows/0/steps/1/correlationId",
            "/workflows/0/steps/1/timeout",
            "/workflows/0/steps/1/dependsOn",
            "/workflows/0/steps/1/parameters/0/in",
            "/workflows/0/steps/1/successCriteria/0/type",
            "/workflows/0/steps/1/successCriteria/1/type/version",
        ], ignoreOrder: true);
    }

    [Fact]
    public void Validate_Arazzo11StepFeaturesInArazzo11Document_ReturnsNoVersionErrors()
    {
        // Act
        var errors = ArazzoValidator.Validate(DocumentUsingEveryArazzo11StepFeature("1.1.0"));

        // Assert
        errors.ShouldNotContain(e => e.Message.Contains("requires Arazzo 1.1.0"));
    }

    [Fact]
    public void Validate_Arazzo11FeaturesInArazzo10ActionsAndComponents_ReturnsErrors()
    {
        // Arrange
        var document = MinimalValidDocument();
        document.Arazzo = "1.0.0";
        var jsonPointer = new ArazzoCriterion { Context = "$response.body", Condition = "/id", Type = "jsonpointer" };
        document.Workflows[0].SuccessActions = [ArazzoReferenceable<ArazzoSuccessAction>.Of(new ArazzoSuccessAction { Name = "done", Type = "end", Criteria = [jsonPointer] })];
        document.Components = new ArazzoComponents
        {
            Parameters = new Dictionary<string, ArazzoParameter> { ["q"] = new() { Name = "q", In = ArazzoParameterLocation.QueryString, Value = "$inputs.q" } },
            FailureActions = new Dictionary<string, ArazzoFailureAction>
            {
                ["stop"] = new() { Name = "stop", Type = "end", Criteria = [jsonPointer] },
            },
        };

        // Act
        var errors = ArazzoValidator.Validate(document);

        // Assert
        errors.Select(e => e.Path).ShouldBe(
        [
            "/workflows/0/successActions/0/criteria/0/type",
            "/components/parameters/q/in",
            "/components/failureActions/stop/criteria/0/type",
        ], ignoreOrder: true);
    }

    [Theory]
    [InlineData("has space")]
    [InlineData("a/b")]
    public void Validate_OutputNameOutsideTheKeyPattern_ReturnsError(string name)
    {
        // Arrange
        var document = MinimalValidDocument();
        document.Workflows[0].Steps[0].Outputs = new Dictionary<string, ArazzoValue> { [name] = "$response.body" };

        // Act
        var errors = ArazzoValidator.Validate(document);

        // Assert
        errors.ShouldHaveSingleItem().Message.ShouldContain("must match");
    }

    [Fact]
    public void Validate_OutputNameWithDotsDashesAndUnderscores_ReturnsNoErrors()
    {
        // Arrange
        var document = MinimalValidDocument();
        document.Workflows[0].Steps[0].Outputs = new Dictionary<string, ArazzoValue> { ["order.id-v2_x"] = "$response.body" };

        // Act & Assert
        ArazzoValidator.Validate(document).ShouldBeEmpty();
    }
}
