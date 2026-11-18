using Bielu.Arazzo.Models;
using Bielu.Arazzo.Readers;
using Bielu.Arazzo.Validation;
using Bielu.Arazzo.Writers;
using Shouldly;
using Xunit;

namespace Bielu.Arazzo.Tests;

public class ArazzoTypedValueTests
{
    [Fact]
    public void ImplicitConversion_FromString_EqualsTheNamedValue()
    {
        // Arrange
        ArazzoStepAction action = "send";
        ArazzoParameterLocation location = "querystring";
        ArazzoSourceDescriptionType type = "asyncapi";
        ArazzoSuccessActionType success = "goto";
        ArazzoFailureActionType failure = "retry";

        // Assert
        action.ShouldBe(ArazzoStepAction.Send);
        location.ShouldBe(ArazzoParameterLocation.QueryString);
        type.ShouldBe(ArazzoSourceDescriptionType.AsyncApi);
        success.ShouldBe(ArazzoSuccessActionType.Goto);
        failure.ShouldBe(ArazzoFailureActionType.Retry);
    }

    [Fact]
    public void Equality_IsCaseSensitive_AsTheSpecificationIs()
    {
        ((ArazzoStepAction)"Send").ShouldNotBe(ArazzoStepAction.Send);
        ((ArazzoStepAction)"Send").IsKnown.ShouldBeFalse();
        ArazzoStepAction.Receive.IsKnown.ShouldBeTrue();
    }

    [Fact]
    public void ValueAndToString_GiveTheStringForm()
    {
        ArazzoParameterLocation.Header.Value.ShouldBe("header");
        ArazzoFailureActionType.End.ToString().ShouldBe("end");
        default(ArazzoStepAction).ToString().ShouldBe(string.Empty);
    }

    [Fact]
    public void Constructor_RejectsNull()
    {
        Should.Throw<ArgumentNullException>(() => new ArazzoStepAction(null!));
        Should.Throw<ArgumentNullException>(() => { ArazzoParameterLocation location = (string)null!; });
    }

    [Fact]
    public void UnknownValues_SurviveARoundTrip_AndAreReportedByTheValidator()
    {
        // Arrange
        var document = new ArazzoDocument
        {
            Arazzo = "1.1.0",
            Info = new ArazzoInfo { Title = "t", Version = "1.0.0" },
            SourceDescriptions = [new ArazzoSourceDescription { Name = "a", Url = "https://example.com/a.json", Type = "graphql" }],
            Workflows =
            [
                new ArazzoWorkflow
                {
                    WorkflowId = "wf",
                    Steps =
                    [
                        new ArazzoStep
                        {
                            StepId = "s",
                            OperationId = "op",
                            Action = "publish",
                            Parameters = [ArazzoReferenceable<ArazzoParameter>.Of(new ArazzoParameter { Name = "p", In = "body", Value = "$inputs.p" })],
                            OnSuccess = [ArazzoReferenceable<ArazzoSuccessAction>.Of(new ArazzoSuccessAction { Name = "x", Type = "stop" })],
                        },
                    ],
                },
            ],
        };

        // Act
        var read = ArazzoStringReader.Read(ArazzoJsonWriter.Write(document)).Document!;
        var errors = ArazzoValidator.Validate(read);

        // Assert
        read.SourceDescriptions[0].Type!.Value.Value.ShouldBe("graphql");
        read.Workflows[0].Steps[0].Action!.Value.Value.ShouldBe("publish");
        errors.ShouldContain(e => e.Message.Contains("Unknown source description type 'graphql'"));
        errors.ShouldContain(e => e.Message.Contains("Unknown step action 'publish'"));
        errors.ShouldContain(e => e.Message.Contains("Unknown parameter location 'body'"));
        errors.ShouldContain(e => e.Message.Contains("Unknown success action type 'stop'"));
    }

    [Fact]
    public void Reader_LeavesAbsentOptionalValuesUnset()
    {
        // Arrange
        const string json = """
            {
              "arazzo": "1.1.0",
              "info": { "title": "t", "version": "1.0.0" },
              "sourceDescriptions": [{ "name": "a", "url": "https://example.com/a.json" }],
              "workflows": [{ "workflowId": "wf", "steps": [{ "stepId": "s", "operationId": "op", "parameters": [{ "name": "p", "value": 1 }] }] }]
            }
            """;

        // Act
        var document = ArazzoStringReader.Read(json).Document!;

        // Assert
        document.SourceDescriptions[0].Type.ShouldBeNull();
        document.Workflows[0].Steps[0].Action.ShouldBeNull();
        document.Workflows[0].Steps[0].Parameters![0].Value!.In.ShouldBeNull();
    }
}
