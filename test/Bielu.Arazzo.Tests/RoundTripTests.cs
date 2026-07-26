using System.Text.Json.Nodes;
using Bielu.Arazzo.Models;
using Bielu.Arazzo.Readers;
using Bielu.Arazzo.Writers;
using Shouldly;
using Xunit;

namespace Bielu.Arazzo.Tests;

public class RoundTripTests
{
    private static ArazzoDocument BuildSampleDocument() => new()
    {
        Arazzo = "1.1.0",
        Self = "https://example.com/workflows/streetlights.arazzo.yaml",
        Info = new ArazzoInfo
        {
            Title = "Streetlights measurement workflow",
            Summary = "Publish a measurement and await the resulting alert",
            Version = "1.0.0",
        },
        SourceDescriptions =
        [
            new ArazzoSourceDescription { Name = "streetlightsApi", Url = "https://example.com/openapi.json", Type = ArazzoSourceDescriptionType.OpenApi },
            new ArazzoSourceDescription { Name = "streetlightsEvents", Url = "https://example.com/asyncapi.json", Type = ArazzoSourceDescriptionType.AsyncApi },
        ],
        Workflows =
        [
            new ArazzoWorkflow
            {
                WorkflowId = "measureAndAlert",
                Summary = "Measure a streetlight and wait for the alert it triggers",
                Inputs = JsonNode.Parse("""{"type":"object","properties":{"lumens":{"type":"integer"}}}"""),
                Steps =
                [
                    new ArazzoStep
                    {
                        StepId = "publishMeasurement",
                        OperationId = "$sourceDescriptions.streetlightsApi.postMeasurement",
                        RequestBody = new ArazzoRequestBody
                        {
                            ContentType = "application/json",
                            Payload = ArazzoValue.FromLiteral(JsonNode.Parse("""{"lumens":"{$inputs.lumens}"}""")),
                        },
                        SuccessCriteria =
                        [
                            new ArazzoCriterion { Condition = "$statusCode == 200" },
                            new ArazzoCriterion { Context = "$response.body", Condition = "$.measurementId", Type = ArazzoCriterionType.JsonPath },
                        ],
                        Outputs = new Dictionary<string, ArazzoValue>
                        {
                            ["measurementId"] = "$response.body#/measurementId",
                        },
                    },
                    new ArazzoStep
                    {
                        StepId = "awaitAlert",
                        ChannelPath = "{$sourceDescriptions.streetlightsEvents.url}#/channels/lightingAlert",
                        Action = ArazzoStepAction.Receive,
                        CorrelationId = "lightingAlertCorrelation",
                        DependsOn = ["publishMeasurement"],
                        SuccessCriteria =
                        [
                            new ArazzoCriterion { Context = "$message.payload", Condition = "$.measurementId == '$steps.publishMeasurement.outputs.measurementId'", Type = ArazzoCriterionType.JsonPath },
                        ],
                        Outputs = new Dictionary<string, ArazzoValue>
                        {
                            ["alertLevel"] = "$message.payload#/level",
                        },
                        Extensions = new Dictionary<string, JsonNode?> { ["x-bielu-note"] = JsonValue.Create("asyncapi step") },
                    },
                ],
                Outputs = new Dictionary<string, ArazzoValue>
                {
                    ["alertLevel"] = "$steps.awaitAlert.outputs.alertLevel",
                },
            },
        ],
        Components = new ArazzoComponents
        {
            Parameters = new Dictionary<string, ArazzoParameter>
            {
                ["page"] = new ArazzoParameter { Name = "page", In = ArazzoParameterLocation.Query, Value = ArazzoValue.FromLiteral(JsonValue.Create(1)) },
            },
        },
    };

    [Fact]
    public void RoundTripsThroughJson()
    {
        var original = BuildSampleDocument();
        var json = ArazzoJsonWriter.Write(original);

        var result = ArazzoStringReader.Read(json);

        result.Diagnostics.Errors.ShouldBeEmpty();
        AssertMatchesSample(result.Document!);
    }

    [Fact]
    public void RoundTripsThroughYaml()
    {
        var original = BuildSampleDocument();
        var yaml = ArazzoYamlWriter.Write(original);

        var result = ArazzoStringReader.Read(yaml);

        result.Diagnostics.Errors.ShouldBeEmpty();
        AssertMatchesSample(result.Document!);
    }

    private static void AssertMatchesSample(ArazzoDocument document)
    {
        document.Arazzo.ShouldBe("1.1.0");
        document.Self.ShouldBe("https://example.com/workflows/streetlights.arazzo.yaml");
        document.Info.Title.ShouldBe("Streetlights measurement workflow");

        document.SourceDescriptions.Count.ShouldBe(2);
        document.SourceDescriptions[1].Type.ShouldBe(ArazzoSourceDescriptionType.AsyncApi);

        document.Workflows.Count.ShouldBe(1);
        var workflow = document.Workflows[0];
        workflow.WorkflowId.ShouldBe("measureAndAlert");
        workflow.Steps.Count.ShouldBe(2);

        var publishStep = workflow.Steps[0];
        publishStep.StepId.ShouldBe("publishMeasurement");
        publishStep.OperationId.ShouldBe("$sourceDescriptions.streetlightsApi.postMeasurement");
        publishStep.SuccessCriteria!.Count.ShouldBe(2);
        publishStep.SuccessCriteria[1].Type!.Type.ShouldBe(ArazzoCriterionType.JsonPath);

        var alertStep = workflow.Steps[1];
        alertStep.StepId.ShouldBe("awaitAlert");
        alertStep.ChannelPath.ShouldBe("{$sourceDescriptions.streetlightsEvents.url}#/channels/lightingAlert");
        alertStep.Action.ShouldBe(ArazzoStepAction.Receive);
        alertStep.CorrelationId.ShouldBe("lightingAlertCorrelation");
        alertStep.DependsOn.ShouldBe(["publishMeasurement"]);
        alertStep.Extensions.ShouldNotBeNull();
        alertStep.Extensions!["x-bielu-note"]!.GetValue<string>().ShouldBe("asyncapi step");

        document.Components.ShouldNotBeNull();
        document.Components!.Parameters!["page"].In.ShouldBe(ArazzoParameterLocation.Query);
        document.Components.Parameters["page"].Value.Literal!.GetValue<int>().ShouldBe(1);
    }
}
