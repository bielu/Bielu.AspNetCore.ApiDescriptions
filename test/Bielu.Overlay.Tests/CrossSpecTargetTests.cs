using System.Text.Json.Nodes;
using Bielu.Overlay;
using Bielu.Overlay.Readers;
using Shouldly;
using Xunit;

namespace Bielu.Overlay.Tests;

/// <summary>
/// The engine sees a <see cref="JsonNode"/> and nothing else, so an AsyncAPI or Arazzo description is as
/// valid an overlay target as an OpenAPI one. These tests exist because that is the claim the library is
/// built on, and because the three specifications stress different parts of the engine.
/// </summary>
/// <remarks>
/// OpenAPI and AsyncAPI key their collections as <b>maps</b> (<c>paths</c>, <c>channels</c>), so targets
/// are plain key lookups. Arazzo keys <c>sourceDescriptions</c>, <c>workflows</c>, and <c>steps</c> as
/// <b>arrays of objects carrying an id field</b>, so every Arazzo target needs an RFC 9535 filter
/// expression, and every removal goes through array index handling rather than a map key. Different code
/// paths, hence separate coverage.
/// </remarks>
public class CrossSpecTargetTests
{
    private static JsonNode Doc(string json) => JsonNode.Parse(json)!;

    private static JsonNode ApplyOk(JsonNode document, string overlay)
    {
        var read = OverlayStringReader.Read(overlay);
        read.HasErrors.ShouldBeFalse($"overlay fixture failed to parse: {string.Join("; ", read.Diagnostics)}");

        var result = OverlayApplier.Apply(document, read.Document!, new OverlayApplyOptions { Strict = true });
        result.Diagnostics.Where(d => !d.IsWarning).ShouldBeEmpty();
        return result.Document!;
    }

    // ---------------------------------------------------------------- AsyncAPI (map-keyed)

    private const string AsyncApiDocument = """
    {
      "asyncapi": "3.0.0",
      "info": { "title": "Streetlights", "version": "1.0.0" },
      "channels": {
        "lightMeasured": { "address": "light/measured" },
        "internalDebug": { "address": "internal/debug" }
      }
    }
    """;

    [Fact]
    public void AsyncApi_ChannelsAreMapKeyed_SoTargetsArePlainLookups()
    {
        var result = ApplyOk(Doc(AsyncApiDocument), """
        overlay: 1.1.0
        info: { title: Public distribution, version: 1.0.0 }
        actions:
          - target: $.channels.internalDebug
            remove: true
          - target: $.info
            update:
              description: Public distribution
        """);

        result["channels"]!.AsObject().ContainsKey("internalDebug").ShouldBeFalse();
        result["channels"]!.AsObject().ContainsKey("lightMeasured").ShouldBeTrue();
        result["info"]!["description"]!.GetValue<string>().ShouldBe("Public distribution");
    }

    // ---------------------------------------------------------------- Arazzo (array-keyed)

    private const string ArazzoDocument = """
    {
      "arazzo": "1.1.0",
      "info": { "title": "Streetlights workflows", "version": "1.0.0" },
      "sourceDescriptions": [
        { "name": "streetlightsApi", "url": "https://example.com/openapi.json", "type": "openapi" }
      ],
      "workflows": [
        {
          "workflowId": "measureAndAlert",
          "summary": "Measure a streetlight and await the alert",
          "steps": [
            { "stepId": "publishMeasurement", "operationId": "$sourceDescriptions.streetlightsApi.postMeasurement" },
            { "stepId": "debugDump",          "operationId": "$sourceDescriptions.streetlightsApi.debugDump" },
            { "stepId": "awaitAlert",         "channelPath": "$sourceDescriptions.events#/channels/alert", "action": "receive" }
          ]
        },
        {
          "workflowId": "internalDiagnostics",
          "summary": "Not for partners",
          "steps": [
            { "stepId": "dumpState", "operationId": "$sourceDescriptions.streetlightsApi.dumpState" }
          ]
        }
      ]
    }
    """;

    [Fact]
    public void Arazzo_RemovesAWorkflowByWorkflowIdFilter()
    {
        // No map key to target — the workflow must be selected by filtering the array on its id.
        var result = ApplyOk(Doc(ArazzoDocument), """
        overlay: 1.1.0
        info: { title: Strip internal workflows, version: 1.0.0 }
        actions:
          - target: $.workflows[?@.workflowId == 'internalDiagnostics']
            remove: true
        """);

        var workflows = result["workflows"]!.AsArray();
        workflows.Count.ShouldBe(1);
        workflows[0]!["workflowId"]!.GetValue<string>().ShouldBe("measureAndAlert");
    }

    [Fact]
    public void Arazzo_RemovesAStepFromANestedArrayByStepIdFilter()
    {
        var result = ApplyOk(Doc(ArazzoDocument), """
        overlay: 1.1.0
        info: { title: Strip debug steps, version: 1.0.0 }
        actions:
          - target: $.workflows[*].steps[?@.stepId == 'debugDump']
            remove: true
        """);

        var steps = result["workflows"]![0]!["steps"]!.AsArray();
        steps.Count.ShouldBe(2);
        steps.Select(s => s!["stepId"]!.GetValue<string>()).ShouldBe(["publishMeasurement", "awaitAlert"]);
    }

    [Fact]
    public void Arazzo_MergesIntoAWorkflowSelectedByFilter()
    {
        var result = ApplyOk(Doc(ArazzoDocument), """
        overlay: 1.1.0
        info: { title: Document the workflow, version: 1.0.0 }
        actions:
          - target: $.workflows[?@.workflowId == 'measureAndAlert']
            update:
              description: Publishes a measurement, then waits for the alert it triggers
        """);

        var workflow = result["workflows"]![0]!;
        workflow["description"]!.GetValue<string>().ShouldBe("Publishes a measurement, then waits for the alert it triggers");
        // A merge, so the existing summary survives.
        workflow["summary"]!.GetValue<string>().ShouldBe("Measure a streetlight and await the alert");
        workflow["steps"]!.AsArray().Count.ShouldBe(3);
    }

    [Fact]
    public void Arazzo_AppendsAStepToAWorkflowSelectedByFilter()
    {
        // Array target + object update = append, reached through a filter.
        var result = ApplyOk(Doc(ArazzoDocument), """
        overlay: 1.1.0
        info: { title: Add an audit step, version: 1.0.0 }
        actions:
          - target: $.workflows[?@.workflowId == 'measureAndAlert'].steps
            update:
              stepId: recordAudit
              operationId: $sourceDescriptions.streetlightsApi.recordAudit
        """);

        var steps = result["workflows"]![0]!["steps"]!.AsArray();
        steps.Count.ShouldBe(4);
        steps[3]!["stepId"]!.GetValue<string>().ShouldBe("recordAudit");
    }

    [Fact]
    public void Arazzo_AppendsASourceDescription()
    {
        var result = ApplyOk(Doc(ArazzoDocument), """
        overlay: 1.1.0
        info: { title: Wire up the event source, version: 1.0.0 }
        actions:
          - target: $.sourceDescriptions
            update:
              name: events
              url: https://example.com/asyncapi.json
              type: asyncapi
        """);

        var sources = result["sourceDescriptions"]!.AsArray();
        sources.Count.ShouldBe(2);
        sources[1]!["type"]!.GetValue<string>().ShouldBe("asyncapi");
    }

    [Fact]
    public void Arazzo_ReplacesAStepFieldViaFilterAndPrimitiveTarget()
    {
        // Combines an array filter with 1.1.0's in-place primitive replacement.
        var result = ApplyOk(Doc(ArazzoDocument), """
        overlay: 1.1.0
        info: { title: Retarget a step, version: 1.0.0 }
        actions:
          - target: $.workflows[*].steps[?@.stepId == 'awaitAlert'].action
            update: send
        """);

        result["workflows"]![0]!["steps"]![2]!["action"]!.GetValue<string>().ShouldBe("send");
    }

    [Fact]
    public void Arazzo_RemovesSeveralStepsAcrossSeveralWorkflows()
    {
        // Matches span two different arrays and shift indexes in both; resolving each index live at
        // removal time is what keeps this correct.
        var document = Doc("""
        {
          "arazzo": "1.1.0",
          "info": { "title": "t", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "api", "url": "u", "type": "openapi" } ],
          "workflows": [
            { "workflowId": "a", "steps": [ {"stepId":"debug"}, {"stepId":"keep"},  {"stepId":"debug"} ] },
            { "workflowId": "b", "steps": [ {"stepId":"keep2"}, {"stepId":"debug"} ] }
          ]
        }
        """);

        var result = ApplyOk(document, """
        overlay: 1.1.0
        info: { title: Strip debug steps everywhere, version: 1.0.0 }
        actions:
          - target: $.workflows[*].steps[?@.stepId == 'debug']
            remove: true
        """);

        result["workflows"]![0]!["steps"]!.AsArray().Select(s => s!["stepId"]!.GetValue<string>()).ShouldBe(["keep"]);
        result["workflows"]![1]!["steps"]!.AsArray().Select(s => s!["stepId"]!.GetValue<string>()).ShouldBe(["keep2"]);
    }

    [Fact]
    public void Arazzo_YamlOverlayAgainstYamlSourcedDocument()
    {
        // Both sides YAML: the document arrives through the shared YamlToJsonNodeConverter, the overlay
        // through the reader, and they meet as one JsonNode tree.
        var yamlDocument = Bielu.Spec.Shared.YamlToJsonNodeConverter.Convert(new StringReader("""
        arazzo: 1.1.0
        info:
          title: Streetlights workflows
          version: 1.0.0
        sourceDescriptions:
          - name: api
            url: https://example.com/openapi.json
            type: openapi
        workflows:
          - workflowId: public
            steps:
              - stepId: one
          - workflowId: internal
            steps:
              - stepId: two
        """))!;

        var result = ApplyOk(yamlDocument, """
        overlay: 1.1.0
        info: { title: Strip internal, version: 1.0.0 }
        actions:
          - target: $.workflows[?@.workflowId == 'internal']
            remove: true
        """);

        result["workflows"]!.AsArray().Count.ShouldBe(1);
        result["workflows"]![0]!["workflowId"]!.GetValue<string>().ShouldBe("public");
    }
}
