using Bielu.Arazzo.Models;
using Bielu.AspNetCore.Arazzo.Services;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.Arazzo.Tests.Unit;

public class ArazzoOptionsTests
{
    [Fact]
    public void AddAsyncApiSource_DefaultsUrlToAsyncApiRoute()
    {
        var options = new ArazzoOptions();

        options.AddAsyncApiSource("events", "v1");

        options.SourceDescriptions.ShouldHaveSingleItem();
        var source = options.SourceDescriptions[0];
        source.Name.ShouldBe("events");
        source.Url.ShouldBe("/asyncapi/v1.json");
        source.Type.ShouldBe(ArazzoSourceDescriptionType.AsyncApi);
    }

    [Fact]
    public void AddOpenApiSource_DefaultsUrlToOpenApiRoute()
    {
        var options = new ArazzoOptions();

        options.AddOpenApiSource("orders", "v1");

        var source = options.SourceDescriptions.ShouldHaveSingleItem();
        source.Url.ShouldBe("/openapi/v1.json");
        source.Type.ShouldBe(ArazzoSourceDescriptionType.OpenApi);
    }

    [Theory]
    [InlineData(null, "v1")]
    [InlineData("", "v1")]
    [InlineData("events", null)]
    [InlineData("events", "")]
    public void AddAsyncApiSource_RejectsNullOrEmptyArguments(string? sourceName, string? asyncApiDocumentName)
    {
        var options = new ArazzoOptions();

        Should.Throw<ArgumentException>(() => options.AddAsyncApiSource(sourceName!, asyncApiDocumentName!));
    }

    [Theory]
    [InlineData(null, "v1")]
    [InlineData("", "v1")]
    [InlineData("orders", null)]
    [InlineData("orders", "")]
    public void AddOpenApiSource_RejectsNullOrEmptyArguments(string? sourceName, string? openApiDocumentName)
    {
        var options = new ArazzoOptions();

        Should.Throw<ArgumentException>(() => options.AddOpenApiSource(sourceName!, openApiDocumentName!));
    }

    [Theory]
    [InlineData("events.v2")]
    [InlineData("events/v2")]
    [InlineData("events v2")]
    public void AddAsyncApiSource_RejectsSourceNameOutsideIdentifierGrammar(string sourceName)
    {
        var options = new ArazzoOptions();

        Should.Throw<ArgumentException>(() => options.AddAsyncApiSource(sourceName, "v1"));
    }

    [Fact]
    public void AddWorkflow_RejectsWorkflowIdOutsideIdentifierGrammar()
    {
        var options = new ArazzoOptions();

        Should.Throw<ArgumentException>(() => options.AddWorkflow("measure.and.alert", _ => { }));
    }

    [Fact]
    public void Step_RejectsStepIdOutsideIdentifierGrammar()
    {
        var options = new ArazzoOptions();

        Should.Throw<ArgumentException>(() => options.AddWorkflow("wf", wf => wf.Step("step.one", _ => { })));
    }

    [Fact]
    public void DependsOn_RejectsNullElement()
    {
        var options = new ArazzoOptions();
        options.AddAsyncApiSource("events", "v1");

        Should.Throw<ArgumentException>(() => options.AddWorkflow("wf", wf => wf
            .Step("a", s => s.Channel("events", "ch", ArazzoStepAction.Send))
            .Step("b", s => s
                .DependsOn("a", null!)
                .Channel("events", "ch2", ArazzoStepAction.Send))));
    }

    [Fact]
    public void AddWorkflow_WithStepsAndOutputs_BuildsExpectedModel()
    {
        var options = new ArazzoOptions();
        options.AddAsyncApiSource("events", "v1");

        options.AddWorkflow("measureAndAlert", wf => wf
            .WithSummary("Measure and alert")
            .Step("publishMeasurement", s => s
                .Channel("events", "lightMeasured", ArazzoStepAction.Send)
                .Output("measurementId", "$message.payload#/id"))
            .Step("awaitAlert", s => s
                .DependsOn("publishMeasurement")
                .Channel("events", "lightingAlert", ArazzoStepAction.Receive)
                .SuccessCriteria("$statusCode == 200")));

        var workflow = options.Workflows.ShouldHaveSingleItem();
        workflow.WorkflowId.ShouldBe("measureAndAlert");
        workflow.Steps.Count.ShouldBe(2);

        var publish = workflow.Steps[0];
        publish.StepId.ShouldBe("publishMeasurement");
        publish.ChannelPath.ShouldBe("{$sourceDescriptions.events.url}#/channels/lightMeasured");
        publish.Action.ShouldBe(ArazzoStepAction.Send);
        publish.Outputs.ShouldNotBeNull();
        publish.Outputs!["measurementId"].Expression.ShouldBe("$message.payload#/id");

        var awaitAlert = workflow.Steps[1];
        awaitAlert.DependsOn.ShouldBe(["publishMeasurement"]);
        awaitAlert.Action.ShouldBe(ArazzoStepAction.Receive);
        awaitAlert.SuccessCriteria.ShouldNotBeNull();
        awaitAlert.SuccessCriteria!.ShouldHaveSingleItem().Condition.ShouldBe("$statusCode == 200");
    }

    [Fact]
    public void AddWorkflow_WithNoSteps_Throws()
    {
        var options = new ArazzoOptions();

        Should.Throw<InvalidOperationException>(() => options.AddWorkflow("empty", _ => { }));
    }

    [Fact]
    public void OperationPath_ProducesPathAndMethodPointer()
    {
        var options = new ArazzoOptions();
        options.AddOpenApiSource("orders", "v1");

        options.AddWorkflow("createOrder", wf => wf
            .Step("create", s => s.OperationPath("orders", "/orders", "post")));

        var step = options.Workflows[0].Steps[0];
        step.OperationPath.ShouldBe("{$sourceDescriptions.orders.url}#/paths/~1orders/post");
    }
}
