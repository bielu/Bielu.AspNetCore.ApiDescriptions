// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bielu.Arazzo.Cli.Commands;
using Bielu.Arazzo.Models;
using Bielu.Cli.Shared.Diff;
using Shouldly;
using Xunit;

namespace Bielu.Arazzo.Cli.Tests;

public class ArazzoDocumentComparerTests
{
    private static ArazzoDocument BuildDocument(IList<ArazzoWorkflow> workflows) => new()
    {
        Arazzo = "1.1.0",
        Info = new ArazzoInfo { Title = "Test", Version = "1.0.0" },
        SourceDescriptions =
        [
            new ArazzoSourceDescription { Name = "api", Url = "https://example.com/openapi.json", Type = ArazzoSourceDescriptionType.OpenApi },
        ],
        Workflows = workflows,
    };

    private static ArazzoWorkflow Workflow(string id, params ArazzoStep[] steps) => new()
    {
        WorkflowId = id,
        Steps = steps,
    };

    private static ArazzoStep Step(string id, string operationId) => new() { StepId = id, OperationId = operationId };

    [Fact]
    public void Compare_RemovedStep_IsBreaking()
    {
        // Arrange
        var @base = BuildDocument([Workflow("wf", Step("a", "op1"), Step("b", "op2"))]);
        var head = BuildDocument([Workflow("wf", Step("a", "op1"))]);

        // Act
        var changes = new ArazzoDocumentComparer().Compare(@base, head).ToList();

        // Assert
        changes.ShouldContain(c => c.Severity == ChangeSeverity.Breaking && c.Message.Contains("Step 'b' was removed"));
    }

    [Fact]
    public void Compare_AddedStep_IsNonBreaking()
    {
        // Arrange
        var @base = BuildDocument([Workflow("wf", Step("a", "op1"))]);
        var head = BuildDocument([Workflow("wf", Step("a", "op1"), Step("b", "op2"))]);

        // Act
        var changes = new ArazzoDocumentComparer().Compare(@base, head).ToList();

        // Assert
        changes.ShouldContain(c => c.Severity == ChangeSeverity.NonBreaking && c.Message.Contains("Step 'b' was added"));
    }

    [Fact]
    public void Compare_StepTargetChanged_IsBreaking()
    {
        // Arrange
        var @base = BuildDocument([Workflow("wf", Step("a", "op1"))]);
        var head = BuildDocument([Workflow("wf", Step("a", "op2"))]);

        // Act
        var changes = new ArazzoDocumentComparer().Compare(@base, head).ToList();

        // Assert
        changes.ShouldContain(c => c.Severity == ChangeSeverity.Breaking && c.Message.Contains("target changed"));
    }

    [Fact]
    public void Compare_RemovedWorkflow_IsBreaking()
    {
        // Arrange
        var @base = BuildDocument([Workflow("wf1", Step("a", "op1")), Workflow("wf2", Step("a", "op1"))]);
        var head = BuildDocument([Workflow("wf1", Step("a", "op1"))]);

        // Act
        var changes = new ArazzoDocumentComparer().Compare(@base, head).ToList();

        // Assert
        changes.ShouldContain(c => c.Severity == ChangeSeverity.Breaking && c.Message.Contains("Workflow 'wf2' was removed"));
    }

    [Fact]
    public void Compare_IdenticalDocuments_ReturnsNoChanges()
    {
        // Arrange
        var @base = BuildDocument([Workflow("wf", Step("a", "op1"))]);
        var head = BuildDocument([Workflow("wf", Step("a", "op1"))]);

        // Act
        var changes = new ArazzoDocumentComparer().Compare(@base, head).ToList();

        // Assert
        changes.ShouldBeEmpty();
    }
}
