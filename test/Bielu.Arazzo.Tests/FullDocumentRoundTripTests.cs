using System.Text.Json.Nodes;
using Bielu.Arazzo.Models;
using Bielu.Arazzo.Readers;
using Bielu.Arazzo.Writers;
using Shouldly;
using Xunit;

namespace Bielu.Arazzo.Tests;

/// <summary>
/// Round-trips a document that uses every object and field of the Arazzo 1.1 model (actions, retries, payload
/// replacements, selectors, reusable references, components, extensions everywhere), and feeds the reader a
/// malformed document to cover its diagnostics.
/// </summary>
public class FullDocumentRoundTripTests
{
    private const string FullDocument = """
        {
          "arazzo": "1.1.0",
          "$self": "https://example.com/full.arazzo.json",
          "info": { "title": "Full", "summary": "Every field", "description": "Kitchen sink", "version": "2.0.0", "x-info": true },
          "sourceDescriptions": [
            { "name": "api", "url": "https://example.com/openapi.json", "type": "openapi", "x-source": 1 },
            { "name": "events", "url": "https://example.com/asyncapi.json", "type": "asyncapi" },
            { "name": "nested", "url": "./other.arazzo.json", "type": "arazzo" }
          ],
          "workflows": [
            {
              "workflowId": "checkout",
              "summary": "Checkout",
              "description": "Places an order",
              "inputs": { "type": "object", "properties": { "sku": { "type": "string" } } },
              "dependsOn": ["$sourceDescriptions.nested.prepare"],
              "parameters": [
                { "name": "tenant", "in": "header", "value": "acme" },
                { "reference": "$components.parameters.page", "value": "2" }
              ],
              "successActions": [
                { "name": "done", "type": "end", "x-action": "yes" },
                { "reference": "$components.successActions.notify" }
              ],
              "failureActions": [
                { "reference": "$components.failureActions.retryTwice" }
              ],
              "steps": [
                {
                  "stepId": "createOrder",
                  "description": "Create the order",
                  "operationPath": "{$sourceDescriptions.api.url}#/paths/~1orders/post",
                  "parameters": [ { "name": "sku", "in": "query", "value": "$inputs.sku" } ],
                  "requestBody": {
                    "contentType": "application/json",
                    "payload": { "sku": "placeholder", "quantity": 1 },
                    "replacements": [
                      { "target": "/sku", "value": "$inputs.sku" },
                      { "target": "$.quantity", "targetSelectorType": "jsonpath", "value": 2, "x-replacement": true },
                      { "target": "/note", "targetSelectorType": { "type": "jsonpointer", "version": "rfc6901" }, "value": { "context": "$inputs", "selector": "/note", "type": "jsonpointer" } }
                    ],
                    "x-body": "b"
                  },
                  "successCriteria": [
                    { "condition": "$statusCode == 201" },
                    { "context": "$response.body", "condition": "$.id", "type": { "type": "jsonpath", "version": "draft-goessner-dispatch-jsonpath-00" } },
                    { "context": "$response.body", "condition": "^ord-", "type": "regex", "x-criterion": 1 }
                  ],
                  "onSuccess": [
                    { "name": "toPayment", "type": "goto", "stepId": "pay", "criteria": [ { "condition": "$statusCode == 201" } ] }
                  ],
                  "onFailure": [
                    { "name": "retry", "type": "retry", "retryAfter": 1.5, "retryLimit": 3, "criteria": [ { "condition": "$statusCode == 503" } ] },
                    { "name": "fallback", "type": "goto", "workflowId": "manualCheckout", "parameters": [ { "name": "reason", "value": "create-failed" } ] }
                  ],
                  "outputs": {
                    "orderId": "$response.body#/id",
                    "status": { "context": "$response.body", "selector": "$.status", "type": "jsonpath" },
                    "literal": 42
                  },
                  "timeout": 30000,
                  "x-step": { "nested": [1, 2] }
                },
                {
                  "stepId": "pay",
                  "operationId": "$sourceDescriptions.api.pay",
                  "dependsOn": ["createOrder"]
                },
                {
                  "stepId": "awaitShipment",
                  "channelPath": "{$sourceDescriptions.events.url}#/channels/shipments",
                  "action": "receive",
                  "correlationId": "orderCorrelation",
                  "timeout": 60000
                },
                {
                  "stepId": "runNested",
                  "workflowId": "$sourceDescriptions.nested.prepare"
                }
              ],
              "outputs": { "orderId": "$steps.createOrder.outputs.orderId" },
              "x-workflow": "w"
            },
            {
              "workflowId": "manualCheckout",
              "steps": [ { "stepId": "ask", "operationId": "$sourceDescriptions.api.ask" } ]
            }
          ],
          "components": {
            "inputs": { "sku": { "type": "string" } },
            "parameters": { "page": { "name": "page", "in": "query", "value": 1 } },
            "successActions": { "notify": { "name": "notify", "type": "goto", "workflowId": "manualCheckout" } },
            "failureActions": { "retryTwice": { "name": "retryTwice", "type": "retry", "retryAfter": 2, "retryLimit": 2 } },
            "x-components": null
          },
          "x-root": "r"
        }
        """;

    private static ArazzoDocument ReadClean(string text)
    {
        var result = ArazzoStringReader.Read(text);
        result.Diagnostics.Errors.ShouldBeEmpty();
        return result.Document.ShouldNotBeNull();
    }

    [Fact]
    public void JsonWriter_FullDocument_RoundTripsToTheSameJson()
    {
        // Arrange
        var original = JsonNode.Parse(FullDocument)!;

        // Act
        var written = ArazzoJsonWriter.Write(ReadClean(FullDocument));

        // Assert: writing what was read reproduces the input, field for field.
        JsonNode.DeepEquals(original, JsonNode.Parse(written)).ShouldBeTrue(written);
    }

    [Fact]
    public void YamlWriter_FullDocument_RoundTripsToTheSameDocument()
    {
        // Arrange
        var original = JsonNode.Parse(FullDocument)!;

        // Act
        var yaml = ArazzoYamlWriter.Write(ReadClean(FullDocument));
        var rewritten = ArazzoJsonWriter.Write(ReadClean(yaml));

        // Assert
        JsonNode.DeepEquals(original, JsonNode.Parse(rewritten)).ShouldBeTrue(yaml);
    }

    [Fact]
    public void Reader_FullDocument_MapsActionsReplacementsAndSelectors()
    {
        // Act
        var document = ReadClean(FullDocument);

        // Assert
        var workflow = document.Workflows[0];
        workflow.Parameters![1].Reference!.Reference.ShouldBe("$components.parameters.page");
        workflow.SuccessActions![1].Reference!.Reference.ShouldBe("$components.successActions.notify");

        var create = workflow.Steps[0];
        create.Timeout.ShouldBe(30000);
        var retry = create.OnFailure![0].Value!;
        retry.RetryAfter.ShouldBe(1.5);
        retry.RetryLimit.ShouldBe(3);
        create.OnFailure[1].Value!.Parameters!.ShouldHaveSingleItem().Value!.Name.ShouldBe("reason");

        var replacements = create.RequestBody!.Replacements!;
        replacements[0].Value.Expression.ShouldBe("$inputs.sku");
        replacements[1].TargetSelectorType!.Type.ShouldBe("jsonpath");
        replacements[2].TargetSelectorType!.Version.ShouldBe("rfc6901");
        replacements[2].Value.Selector!.Selector.ShouldBe("/note");

        create.Outputs!["status"].Selector!.Type.Type.ShouldBe("jsonpath");
        create.Outputs["literal"].Literal!.GetValue<int>().ShouldBe(42);

        document.Components!.FailureActions!["retryTwice"].RetryLimit.ShouldBe(2);
        document.Components.Inputs!["sku"]!["type"]!.GetValue<string>().ShouldBe("string");
        document.Extensions!["x-root"]!.GetValue<string>().ShouldBe("r");
    }

    [Fact]
    public void Reader_NonObjectRoot_ReportsErrorAndReturnsEmptyDocument()
    {
        // Act
        var result = ArazzoStringReader.Read("[1, 2]");

        // Assert
        result.Diagnostics.Errors.ShouldContain(error => error.Path == "/");
        result.Document.ShouldNotBeNull().Workflows.ShouldBeEmpty();
    }

    [Fact]
    public void Reader_MalformedObjects_ReportsAPointerForEachProblem()
    {
        // Arrange: every collection holds a wrongly-typed entry, and required fields are missing.
        const string malformed = """
            {
              "arazzo": "1.1.0",
              "info": "not an object",
              "sourceDescriptions": [ 1 ],
              "workflows": [
                2,
                {
                  "workflowId": "w",
                  "parameters": [ 3 ],
                  "successActions": [ 4 ],
                  "failureActions": [ 5 ],
                  "steps": [
                    6,
                    {
                      "stepId": "s",
                      "timeout": 1.5,
                      "requestBody": { "replacements": [ 7 ] },
                      "successCriteria": [ 8, { "context": "$response.body" } ],
                      "onFailure": [ { "name": "r", "type": "retry", "retryLimit": 99999999999 } ]
                    }
                  ]
                }
              ],
              "components": {
                "parameters": { "p": 9 },
                "successActions": { "s": 10 },
                "failureActions": { "f": 11 }
              }
            }
            """;

        // Act
        var result = ArazzoStringReader.Read(malformed);

        // Assert
        var pointers = result.Diagnostics.Errors.Select(error => error.Path).ToList();
        pointers.ShouldContain("/info");
        pointers.ShouldContain("/sourceDescriptions/0");
        pointers.ShouldContain("/workflows/0");
        pointers.ShouldContain("/workflows/1/parameters/0");
        pointers.ShouldContain("/workflows/1/successActions/0");
        pointers.ShouldContain("/workflows/1/failureActions/0");
        pointers.ShouldContain("/workflows/1/steps/0");
        pointers.ShouldContain("/workflows/1/steps/1/timeout");
        pointers.ShouldContain("/workflows/1/steps/1/requestBody/replacements/0");
        pointers.ShouldContain("/workflows/1/steps/1/successCriteria/0");
        pointers.ShouldContain("/workflows/1/steps/1/successCriteria/1/condition");
        pointers.ShouldContain("/workflows/1/steps/1/onFailure/0/retryLimit");
        pointers.ShouldContain("/components/parameters/p");
        pointers.ShouldContain("/components/successActions/s");
        pointers.ShouldContain("/components/failureActions/f");
    }
}
