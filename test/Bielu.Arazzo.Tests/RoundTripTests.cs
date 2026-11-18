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
    public void WriteAndRead_JsonDocument_RoundTrips()
    {
        var original = BuildSampleDocument();
        var json = ArazzoJsonWriter.Write(original);

        var result = ArazzoStringReader.Read(json);

        result.Diagnostics.Errors.ShouldBeEmpty();
        AssertMatchesSample(result.Document!);
    }

    [Fact]
    public void WriteAndRead_YamlDocument_RoundTrips()
    {
        var original = BuildSampleDocument();
        var yaml = ArazzoYamlWriter.Write(original);

        var result = ArazzoStringReader.Read(yaml);

        result.Diagnostics.Errors.ShouldBeEmpty();
        AssertMatchesSample(result.Document!);
    }

    private const string ActionParametersJson = """
        {
          "arazzo": "1.1.0",
          "info": { "title": "t", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "api", "url": "https://example.com/openapi.json", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "placeOrder",
              "steps": [
                {
                  "stepId": "create",
                  "operationId": "$sourceDescriptions.api.createOrder",
                  "onSuccess": [
                    {
                      "name": "track",
                      "type": "goto",
                      "workflowId": "trackOrder",
                      "parameters": [
                        { "name": "orderId", "value": "$response.body#/id" },
                        { "reference": "$components.parameters.tenant" }
                      ]
                    }
                  ],
                  "onFailure": [
                    {
                      "name": "recover",
                      "type": "retry",
                      "workflowId": "refreshToken",
                      "parameters": [ { "name": "scope", "value": "orders" } ]
                    }
                  ]
                }
              ]
            },
            { "workflowId": "trackOrder", "steps": [ { "stepId": "get", "operationId": "$sourceDescriptions.api.getOrder" } ] },
            { "workflowId": "refreshToken", "steps": [ { "stepId": "refresh", "operationId": "$sourceDescriptions.api.refresh" } ] }
          ],
          "components": {
            "parameters": { "tenant": { "name": "tenant", "value": "$inputs.tenant" } },
            "successActions": {
              "track": { "name": "track", "type": "goto", "workflowId": "trackOrder", "parameters": [ { "name": "orderId", "value": "$inputs.orderId" } ] }
            }
          }
        }
        """;

    [Theory]
    [InlineData("json")]
    [InlineData("yaml")]
    public void WriteAndRead_Arazzo11ActionParameters_RoundTrip(string format)
    {
        // Arrange
        var read = ArazzoStringReader.Read(ActionParametersJson);
        read.Diagnostics.Errors.ShouldBeEmpty();

        // Act
        var serialized = format == "json" ? ArazzoJsonWriter.Write(read.Document!) : ArazzoYamlWriter.Write(read.Document!);
        var result = ArazzoStringReader.Read(serialized);

        // Assert
        result.Diagnostics.Errors.ShouldBeEmpty();
        var step = result.Document!.Workflows[0].Steps[0];
        var onSuccess = step.OnSuccess!.ShouldHaveSingleItem().Value!;
        onSuccess.Extensions.ShouldBeNull();
        onSuccess.Parameters!.Count.ShouldBe(2);
        onSuccess.Parameters[0].Value!.Name.ShouldBe("orderId");
        onSuccess.Parameters[0].Value!.Value.Expression.ShouldBe("$response.body#/id");
        onSuccess.Parameters[1].Reference!.Reference.ShouldBe("$components.parameters.tenant");
        var onFailure = step.OnFailure!.ShouldHaveSingleItem().Value!;
        onFailure.Parameters!.ShouldHaveSingleItem().Value!.Value.Literal!.GetValue<string>().ShouldBe("orders");
        var component = result.Document.Components!.SuccessActions!["track"];
        component.Parameters!.ShouldHaveSingleItem().Value!.Value.Expression.ShouldBe("$inputs.orderId");
        Bielu.Arazzo.Validation.ArazzoValidator.Validate(result.Document).ShouldBeEmpty();
    }

    [Fact]
    public void Read_Arazzo10YamlWithActionParameters_ReadsThemAndFailsValidation()
    {
        // Arrange
        const string yaml = """
            arazzo: 1.0.1
            info: { title: t, version: 1.0.0 }
            sourceDescriptions:
              - { name: api, url: https://example.com/openapi.json, type: openapi }
            workflows:
              - workflowId: wf
                steps:
                  - stepId: s1
                    operationId: $sourceDescriptions.api.op
                    onSuccess:
                      - name: again
                        type: goto
                        workflowId: wf
                        parameters:
                          - { name: id, value: $response.body#/id }
            """;

        // Act
        var result = ArazzoStringReader.Read(yaml);
        var errors = Bielu.Arazzo.Validation.ArazzoValidator.Validate(result.Document!);

        // Assert
        result.Diagnostics.Errors.ShouldBeEmpty();
        result.Document!.Workflows[0].Steps[0].OnSuccess![0].Value!.Parameters.ShouldHaveSingleItem();
        errors.ShouldHaveSingleItem().Path.ShouldBe("/workflows/0/steps/0/onSuccess/0/parameters");
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
