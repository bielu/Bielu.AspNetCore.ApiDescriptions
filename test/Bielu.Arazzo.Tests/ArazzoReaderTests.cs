using Bielu.Arazzo.Readers;
using Shouldly;
using Xunit;

namespace Bielu.Arazzo.Tests;

public class ArazzoReaderTests
{
    [Fact]
    public void Read_ComponentMapsWithNonObjectValues_ReturnsErrors()
    {
        // Arrange
        const string content = """
            {
              "arazzo": "1.1.0",
              "info": { "title": "t", "version": "1.0.0" },
              "sourceDescriptions": [],
              "workflows": [],
              "components": {
                "parameters": [],
                "successActions": "bad",
                "failureActions": true
              }
            }
            """;

        // Act
        var result = ArazzoStringReader.Read(content);

        // Assert
        result.Diagnostics.Errors.ShouldContain(e => e.Path == "/components/parameters" && e.Message.Contains("must be an object"));
        result.Diagnostics.Errors.ShouldContain(e => e.Path == "/components/successActions" && e.Message.Contains("must be an object"));
        result.Diagnostics.Errors.ShouldContain(e => e.Path == "/components/failureActions" && e.Message.Contains("must be an object"));
    }

    [Fact]
    public void Read_RequestBodyWithNonObjectValue_ReturnsError()
    {
        // Arrange
        const string content = """
            {
              "arazzo": "1.1.0",
              "info": { "title": "t", "version": "1.0.0" },
              "sourceDescriptions": [],
              "workflows": [
                {
                  "workflowId": "wf",
                  "steps": [
                    { "stepId": "s1", "operationId": "op", "requestBody": "bad" }
                  ]
                }
              ]
            }
            """;

        // Act
        var result = ArazzoStringReader.Read(content);

        // Assert
        result.Diagnostics.Errors.ShouldContain(e => e.Path == "/workflows/0/steps/0/requestBody" && e.Message.Contains("must be an object"));
        result.Document!.Workflows[0].Steps[0].RequestBody.ShouldBeNull();
    }

    [Fact]
    public void Read_FractionalAndOutOfRangeIntegerFields_ReturnsErrorsAndNullValues()
    {
        // Arrange
        const string content = """
            {
              "arazzo": "1.1.0",
              "info": { "title": "t", "version": "1.0.0" },
              "sourceDescriptions": [],
              "workflows": [
                {
                  "workflowId": "wf",
                  "steps": [
                    {
                      "stepId": "s1",
                      "operationId": "op",
                      "timeout": 1.5,
                      "onFailure": [
                        { "name": "retry", "type": "retry", "stepId": "s1", "retryLimit": 2147483648 }
                      ]
                    }
                  ]
                }
              ]
            }
            """;

        // Act
        var result = ArazzoStringReader.Read(content);

        // Assert
        result.Diagnostics.Errors.ShouldContain(e => e.Path == "/workflows/0/steps/0/timeout" && e.Message.Contains("must be an integer"));
        result.Diagnostics.Errors.ShouldContain(e => e.Path == "/workflows/0/steps/0/onFailure/0/retryLimit" && e.Message.Contains("must be an integer"));
        result.Document!.Workflows[0].Steps[0].Timeout.ShouldBeNull();
        result.Document.Workflows[0].Steps[0].OnFailure![0].Value!.RetryLimit.ShouldBeNull();
    }
}
