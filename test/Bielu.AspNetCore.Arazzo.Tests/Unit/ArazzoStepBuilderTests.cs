using Bielu.Arazzo.Models;
using Bielu.AspNetCore.Arazzo;
using Bielu.AspNetCore.Arazzo.Extensions;
using Bielu.AspNetCore.Arazzo.Services;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.Arazzo.Tests.Unit;

public class ArazzoStepBuilderTests
{
    private static ArazzoOptions BuildOptionsWithStep(Action<ArazzoStepBuilder> configureStep)
    {
        var options = new ArazzoOptions();
        options.AddOpenApiSource("api", "v1");
        options.AddAsyncApiSource("events", "v1");
        options.AddWorkflow("wf", wf => wf.Step("step", configureStep));
        return options;
    }

    [Fact]
    public void Operation_ThenChannel_ThrowsAndKeepsOriginalTarget()
    {
        var options = BuildOptionsWithStep(s =>
        {
            s.Operation("createOrder");
            Should.Throw<InvalidOperationException>(() => s.Channel("events", "lightMeasured", ArazzoStepAction.Send));
        });

        var step = options.Workflows[0].Steps[0];
        step.OperationId.ShouldBe("createOrder");
        step.ChannelPath.ShouldBeNull();
    }

    [Fact]
    public void Channel_ThenOperationPath_ThrowsAndKeepsOriginalTarget()
    {
        var options = BuildOptionsWithStep(s =>
        {
            s.Channel("events", "lightMeasured", ArazzoStepAction.Send);
            Should.Throw<InvalidOperationException>(() => s.OperationPath("api", "/orders", "post"));
        });

        var step = options.Workflows[0].Steps[0];
        step.ChannelPath.ShouldNotBeNull();
        step.OperationPath.ShouldBeNull();
    }

    [Fact]
    public void Workflow_ThenOperation_ThrowsAndKeepsOriginalTarget()
    {
        var options = BuildOptionsWithStep(s =>
        {
            s.Workflow("otherWorkflow");
            Should.Throw<InvalidOperationException>(() => s.Operation("createOrder"));
        });

        var step = options.Workflows[0].Steps[0];
        step.WorkflowId.ShouldBe("otherWorkflow");
        step.OperationId.ShouldBeNull();
    }

    [Fact]
    public void Operation_CalledTwice_Throws()
    {
        var options = BuildOptionsWithStep(s =>
        {
            s.Operation("createOrder");
            Should.Throw<InvalidOperationException>(() => s.Operation("cancelOrder"));
        });

        options.Workflows[0].Steps[0].OperationId.ShouldBe("createOrder");
    }

    [Fact]
    public void Operation_RejectedSecondCallWithInvalidArgument_DoesNotMutateState()
    {
        var options = BuildOptionsWithStep(s =>
        {
            s.Operation("createOrder");
            Should.Throw<ArgumentException>(() => s.OperationPath("", "/orders", "post"));
        });

        var step = options.Workflows[0].Steps[0];
        step.OperationId.ShouldBe("createOrder");
        step.OperationPath.ShouldBeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void WithTimeout_RejectsNonPositiveValues(int milliseconds)
    {
        BuildOptionsWithStep(s => Should.Throw<ArgumentOutOfRangeException>(() => s.WithTimeout(milliseconds)));
    }

    [Fact]
    public async Task StepWithNoTarget_FailsAtDocumentValidation_NotAtTheBuilder()
    {
        // The builder only enforces the upper bound (at most one target); the lower bound (at least
        // one target) is the shared ArazzoValidator's job when the document is materialized, so it
        // applies uniformly whether a document was built via this fluent API or some other producer.
        var services = new ServiceCollection();
        services.AddArazzo(options =>
        {
            options.WithInfo("Test", "1.0.0");
            options.AddWorkflow("wf", wf => wf.Step("step", _ => { }));
        });
        var provider = services.BuildServiceProvider();
        var documentProvider = provider.GetRequiredKeyedService<IArazzoDocumentProvider>(ArazzoDefaults.DefaultDocumentName);

        await Should.ThrowAsync<ArazzoDocumentValidationException>(() => documentProvider.GetArazzoDocumentAsync());
    }

    [Fact]
    public void FluentMethods_PopulateEveryStepField()
    {
        // Arrange
        var success = new ArazzoSuccessAction { Name = "done", Type = "end" };
        var failure = new ArazzoFailureAction { Name = "retry", Type = "retry", RetryAfter = 1, RetryLimit = 3 };

        // Act
        var options = BuildOptionsWithStep(s => s
            .Channel("events", "lightMeasured", ArazzoStepAction.Receive)
            .WithDescription("Wait for the measurement")
            .WithCorrelationId("measurementCorrelation")
            .Parameter("tenant", "$inputs.tenant", "header")
            .OnSuccess(success)
            .OnFailure(failure)
            .WithTimeout(5000));

        // Assert
        var step = options.Workflows[0].Steps[0];
        step.Description.ShouldBe("Wait for the measurement");
        step.CorrelationId.ShouldBe("measurementCorrelation");
        var parameter = step.Parameters.ShouldNotBeNull().ShouldHaveSingleItem().Value.ShouldNotBeNull();
        parameter.Name.ShouldBe("tenant");
        parameter.In.ShouldBe("header");
        parameter.Value.Expression.ShouldBe("$inputs.tenant");
        step.OnSuccess.ShouldNotBeNull().ShouldHaveSingleItem().Value.ShouldBeSameAs(success);
        step.OnFailure.ShouldNotBeNull().ShouldHaveSingleItem().Value.ShouldBeSameAs(failure);
        step.Timeout.ShouldBe(5000);
    }

    [Fact]
    public void Payload_LiteralAndExpression_SetTheRequestBody()
    {
        // Act
        var literal = BuildOptionsWithStep(s => s.Operation("createOrder").Payload(System.Text.Json.Nodes.JsonNode.Parse("""{"sku":"A"}""")!, "application/json"));
        var expression = BuildOptionsWithStep(s => s.Operation("createOrder").PayloadExpression("$inputs.order"));

        // Assert
        var literalBody = literal.Workflows[0].Steps[0].RequestBody.ShouldNotBeNull();
        literalBody.ContentType.ShouldBe("application/json");
        literalBody.Payload.ShouldNotBeNull().Literal!["sku"]!.GetValue<string>().ShouldBe("A");
        var expressionBody = expression.Workflows[0].Steps[0].RequestBody.ShouldNotBeNull();
        expressionBody.ContentType.ShouldBeNull();
        expressionBody.Payload.ShouldNotBeNull().Expression.ShouldBe("$inputs.order");
    }

    [Fact]
    public void FluentMethods_InvalidArguments_Throw()
    {
        // Act & Assert
        BuildOptionsWithStep(s =>
        {
            Should.Throw<ArgumentException>(() => s.WithCorrelationId(""));
            Should.Throw<ArgumentNullException>(() => s.Payload(null!));
            Should.Throw<ArgumentException>(() => s.PayloadExpression(""));
            Should.Throw<ArgumentException>(() => s.Parameter("", "$inputs.x"));
            Should.Throw<ArgumentException>(() => s.Parameter("x", ""));
            Should.Throw<ArgumentNullException>(() => s.OnSuccess(null!));
            Should.Throw<ArgumentNullException>(() => s.OnFailure(null!));
            Should.Throw<ArgumentOutOfRangeException>(() => s.WithTimeout(0));
            s.Operation("createOrder");
        });
    }
}
