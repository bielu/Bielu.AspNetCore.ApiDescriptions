using Bielu.Arazzo.Models;
using Bielu.AspNetCore.Arazzo.Extensions;
using Bielu.AspNetCore.Arazzo.Services;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.Arazzo.Tests.Unit;

public class ArazzoCrossDocumentWorkflowTests
{
    [Fact]
    public async Task Workflow_OfAnotherArazzoDocument_TargetsItThroughItsSourceDescription()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddArazzo(options => options
            .WithInfo("Checkout", "1.0.0")
            .AddSourceDescription("payments", "https://example.com/payments.arazzo.json", ArazzoSourceDescriptionType.Arazzo)
            .AddWorkflow("checkout", wf => wf.Step("pay", s => s
                .Workflow("payments", "chargeCard")
                .Parameter("amount", "$inputs.amount"))));
        var provider = services.BuildServiceProvider().GetRequiredKeyedService<IArazzoDocumentProvider>(ArazzoDefaults.DefaultDocumentName);

        // Act
        var document = await provider.GetArazzoDocumentAsync();

        // Assert
        var step = document.Workflows[0].Steps[0];
        step.WorkflowId.ShouldBe("$sourceDescriptions.payments.chargeCard");
        step.OperationId.ShouldBeNull();
    }

    [Theory]
    [InlineData("", "chargeCard")]
    [InlineData("pay ments", "chargeCard")]
    [InlineData("payments", "")]
    [InlineData("payments", "charge.card")]
    public void Workflow_OfAnotherArazzoDocument_RejectsInvalidIdentifiers(string sourceName, string workflowId)
    {
        new ArazzoOptions().AddWorkflow("wf", wf => wf.Step("s", s =>
        {
            Should.Throw<ArgumentException>(() => s.Workflow(sourceName, workflowId));
            s.Workflow("local");
        }));
    }

    [Fact]
    public void Workflow_OfAnotherArazzoDocument_IsMutuallyExclusiveWithOtherTargets()
    {
        new ArazzoOptions().AddWorkflow("wf", wf => wf.Step("s", s =>
        {
            s.Operation("createOrder");
            Should.Throw<InvalidOperationException>(() => s.Workflow("payments", "chargeCard"));
        }));
    }
}
