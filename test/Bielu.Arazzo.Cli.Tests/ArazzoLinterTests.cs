// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bielu.Arazzo.Cli.Commands;
using Bielu.Arazzo.Models;
using Shouldly;
using Xunit;

namespace Bielu.Arazzo.Cli.Tests;

public class ArazzoLinterTests
{
    private static ArazzoDocument BuildDocument(IList<ArazzoWorkflow> workflows, ArazzoComponents? components = null) =>
        new()
        {
            Arazzo = "1.1.0",
            Info = new ArazzoInfo { Title = "Test", Summary = "A summary", Version = "1.0.0" },
            SourceDescriptions =
            [
                new ArazzoSourceDescription { Name = "api", Url = "https://example.com/openapi.json", Type = ArazzoSourceDescriptionType.OpenApi },
            ],
            Workflows = workflows,
            Components = components,
        };

    [Fact]
    public void Lint_StepWithSelfReferentialDependsOn_ReportsCycle()
    {
        // Arrange
        var document = BuildDocument(
        [
            new ArazzoWorkflow
            {
                WorkflowId = "wf",
                Summary = "wf",
                Steps =
                [
                    new ArazzoStep { StepId = "a", Description = "d", OperationId = "op", DependsOn = ["b"] },
                    new ArazzoStep { StepId = "b", Description = "d", OperationId = "op", DependsOn = ["a"] },
                ],
            },
        ]);

        // Act
        var findings = ArazzoLinter.Lint(document);

        // Assert
        findings.ShouldContain(f => !f.IsWarning && f.Message.Contains("Circular dependsOn"));
    }

    [Fact]
    public void Lint_StepDependsOnUnknownStepId_ReportsError()
    {
        // Arrange
        var document = BuildDocument(
        [
            new ArazzoWorkflow
            {
                WorkflowId = "wf",
                Summary = "wf",
                Steps =
                [
                    new ArazzoStep { StepId = "a", Description = "d", OperationId = "op", DependsOn = ["ghost"] },
                ],
            },
        ]);

        // Act
        var findings = ArazzoLinter.Lint(document);

        // Assert
        findings.ShouldContain(f => !f.IsWarning && f.Message.Contains("unknown stepId 'ghost'"));
    }

    [Fact]
    public void Lint_WorkflowMissingSummaryAndDescription_ReportsWarning()
    {
        // Arrange
        var document = BuildDocument(
        [
            new ArazzoWorkflow
            {
                WorkflowId = "wf",
                Steps = [new ArazzoStep { StepId = "a", Description = "d", OperationId = "op" }],
            },
        ]);

        // Act
        var findings = ArazzoLinter.Lint(document);

        // Assert
        findings.ShouldContain(f => f.IsWarning && f.Message.Contains("neither a summary nor a description"));
    }

    [Fact]
    public void Lint_UnreferencedComponent_ReportsWarning()
    {
        // Arrange
        var document = BuildDocument(
            [
                new ArazzoWorkflow
                {
                    WorkflowId = "wf",
                    Summary = "wf",
                    Steps = [new ArazzoStep { StepId = "a", Description = "d", OperationId = "op" }],
                },
            ],
            new ArazzoComponents
            {
                Parameters = new Dictionary<string, ArazzoParameter>
                {
                    ["unused"] = new() { Name = "page", In = ArazzoParameterLocation.Query, Value = ArazzoValue.FromLiteral(System.Text.Json.Nodes.JsonValue.Create(1)) },
                },
            });

        // Act
        var findings = ArazzoLinter.Lint(document);

        // Assert
        findings.ShouldContain(f => f.IsWarning && f.Message.Contains("'unused'") && f.Message.Contains("never referenced"));
    }

    [Fact]
    public void Lint_ReferencedComponent_DoesNotReportUnused()
    {
        // Arrange
        var document = BuildDocument(
            [
                new ArazzoWorkflow
                {
                    WorkflowId = "wf",
                    Summary = "wf",
                    Steps = [new ArazzoStep { StepId = "a", Description = "d", OperationId = "op" }],
                    Parameters =
                    [
                        ArazzoReferenceable<ArazzoParameter>.Of(new ArazzoReusableObject { Reference = "$components.parameters.used" }),
                    ],
                },
            ],
            new ArazzoComponents
            {
                Parameters = new Dictionary<string, ArazzoParameter>
                {
                    ["used"] = new() { Name = "page", In = ArazzoParameterLocation.Query, Value = ArazzoValue.FromLiteral(System.Text.Json.Nodes.JsonValue.Create(1)) },
                },
            });

        // Act
        var findings = ArazzoLinter.Lint(document);

        // Assert
        findings.ShouldNotContain(f => f.Message.Contains("never referenced"));
    }

    [Fact]
    public void Lint_FullyDocumentedCleanDocument_ReturnsNoFindings()
    {
        // Arrange
        var document = BuildDocument(
        [
            new ArazzoWorkflow
            {
                WorkflowId = "doThing",
                Summary = "Does a thing",
                Steps = [new ArazzoStep { StepId = "step1", Description = "first step", OperationId = "op" }],
            },
        ]);

        // Act
        var findings = ArazzoLinter.Lint(document);

        // Assert
        findings.ShouldBeEmpty();
    }

    [Fact]
    public void Lint_NonPortableIdentifiers_ReportWarnings()
    {
        // Arrange
        var document = BuildDocument(
        [
            new ArazzoWorkflow
            {
                WorkflowId = "place order",
                Summary = "Places an order",
                Steps = [new ArazzoStep { StepId = "step.one", Description = "d", OperationId = "op" }],
            },
        ]);

        // Act
        var findings = ArazzoLinter.Lint(document);

        // Assert
        findings.ShouldContain(f => f.IsWarning && f.Path == "/workflows/0" && f.Message.Contains("workflowId 'place order'"));
        findings.ShouldContain(f => f.IsWarning && f.Path == "/workflows/0/steps/0" && f.Message.Contains("stepId 'step.one'"));
    }

    [Fact]
    public void Lint_WorkflowDependsOnUnknownWorkflow_ReportsError()
    {
        // Arrange
        var document = BuildDocument(
        [
            new ArazzoWorkflow
            {
                WorkflowId = "wf",
                Summary = "s",
                DependsOn = ["missing"],
                Steps = [new ArazzoStep { StepId = "a", Description = "d", OperationId = "op" }],
            },
        ]);

        // Act
        var findings = ArazzoLinter.Lint(document);

        // Assert
        findings.ShouldContain(f => !f.IsWarning && f.Path == "/workflows/0/dependsOn" && f.Message.Contains("unknown workflowId 'missing'"));
    }

    [Fact]
    public void Lint_CircularWorkflowDependsOn_ReportsCycle()
    {
        // Arrange
        ArazzoWorkflow Workflow(string id, string dependsOn) => new()
        {
            WorkflowId = id,
            Summary = "s",
            DependsOn = [dependsOn],
            Steps = [new ArazzoStep { StepId = "a", Description = "d", OperationId = "op" }],
        };
        var document = BuildDocument([Workflow("first", "second"), Workflow("second", "first")]);

        // Act
        var findings = ArazzoLinter.Lint(document);

        // Assert
        findings.ShouldContain(f => !f.IsWarning && f.Path == "/workflows" && f.Message.StartsWith("Circular dependsOn detected"));
    }
}
