// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;
using Bielu.Arazzo.Models;
using Bielu.Arazzo.Validation;

namespace Bielu.Arazzo.Cli.Commands;

/// <summary>
/// Style and graph-shape checks beyond <see cref="ArazzoValidator"/>'s structural invariants: missing
/// documentation, identifier characters that don't travel well across tooling, circular <c>dependsOn</c>
/// graphs, dangling same-document <c>dependsOn</c>/workflow references, and <c>components</c> entries that
/// are declared but never referenced. Resolving references against the actual source documents (does this
/// <c>operationId</c> really exist?) needs a live app and belongs to <c>Bielu.AspNetCore.Arazzo</c>'s
/// <c>IArazzoSourceResolver</c>, not this offline, CLI-only pass.
/// </summary>
internal static class ArazzoLinter
{
    private static readonly Regex PortableIdentifier = new("^[A-Za-z0-9_-]+$", RegexOptions.Compiled);

    public static IReadOnlyList<ArazzoError> Lint(ArazzoDocument document)
    {
        var findings = new List<ArazzoError>();

        LintInfo(document, findings);
        LintWorkflows(document, findings);
        LintComponentUsage(document, findings);

        return findings;
    }

    private static void LintInfo(ArazzoDocument document, List<ArazzoError> findings)
    {
        if (string.IsNullOrWhiteSpace(document.Info.Summary) && string.IsNullOrWhiteSpace(document.Info.Description))
        {
            findings.Add(new ArazzoError("/info", "Document has neither a summary nor a description.", IsWarning: true));
        }
    }

    private static void LintWorkflows(ArazzoDocument document, List<ArazzoError> findings)
    {
        var workflowIds = new HashSet<string>(document.Workflows.Select(w => w.WorkflowId), StringComparer.Ordinal);
        var workflowGraph = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        for (var w = 0; w < document.Workflows.Count; w++)
        {
            var workflow = document.Workflows[w];
            var workflowPath = $"/workflows/{w}";
            var dependencies = new List<string>();
            workflowGraph[workflow.WorkflowId] = dependencies;

            if (!PortableIdentifier.IsMatch(workflow.WorkflowId))
            {
                findings.Add(new ArazzoError(workflowPath,
                    $"workflowId '{workflow.WorkflowId}' contains characters that may not be portable across tooling; stick to letters, digits, '-', and '_'.",
                    IsWarning: true));
            }

            if (string.IsNullOrWhiteSpace(workflow.Summary) && string.IsNullOrWhiteSpace(workflow.Description))
            {
                findings.Add(new ArazzoError(workflowPath,
                    $"Workflow '{workflow.WorkflowId}' has neither a summary nor a description.", IsWarning: true));
            }

            if (workflow.DependsOn is not null)
            {
                foreach (var dependsOnId in workflow.DependsOn)
                {
                    if (!workflowIds.Contains(dependsOnId))
                    {
                        findings.Add(new ArazzoError($"{workflowPath}/dependsOn",
                            $"Workflow '{workflow.WorkflowId}' depends on unknown workflowId '{dependsOnId}'."));
                        continue;
                    }

                    dependencies.Add(dependsOnId);
                }
            }

            LintSteps(workflow, workflowPath, findings);
        }

        var workflowCycle = FindCycle(workflowGraph);
        if (workflowCycle is not null)
        {
            findings.Add(new ArazzoError("/workflows",
                $"Circular dependsOn detected: {string.Join(" -> ", workflowCycle)}."));
        }
    }

    private static void LintSteps(ArazzoWorkflow workflow, string workflowPath, List<ArazzoError> findings)
    {
        var stepIds = new HashSet<string>(workflow.Steps.Select(s => s.StepId), StringComparer.Ordinal);
        var stepGraph = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        for (var s = 0; s < workflow.Steps.Count; s++)
        {
            var step = workflow.Steps[s];
            var stepPath = $"{workflowPath}/steps/{s}";
            var dependencies = new List<string>();
            stepGraph[step.StepId] = dependencies;

            if (!PortableIdentifier.IsMatch(step.StepId))
            {
                findings.Add(new ArazzoError(stepPath,
                    $"stepId '{step.StepId}' contains characters that may not be portable across tooling; stick to letters, digits, '-', and '_'.",
                    IsWarning: true));
            }

            if (string.IsNullOrWhiteSpace(step.Description))
            {
                findings.Add(new ArazzoError(stepPath, $"Step '{step.StepId}' has no description.", IsWarning: true));
            }

            if (step.DependsOn is not null)
            {
                foreach (var dependsOnId in step.DependsOn)
                {
                    if (!stepIds.Contains(dependsOnId))
                    {
                        findings.Add(new ArazzoError($"{stepPath}/dependsOn",
                            $"Step '{step.StepId}' depends on unknown stepId '{dependsOnId}' in workflow '{workflow.WorkflowId}'."));
                        continue;
                    }

                    dependencies.Add(dependsOnId);
                }
            }
        }

        var stepCycle = FindCycle(stepGraph);
        if (stepCycle is not null)
        {
            findings.Add(new ArazzoError(workflowPath,
                $"Circular dependsOn detected in workflow '{workflow.WorkflowId}': {string.Join(" -> ", stepCycle)}."));
        }
    }

    private static void LintComponentUsage(ArazzoDocument document, List<ArazzoError> findings)
    {
        if (document.Components is null)
        {
            return;
        }

        var referenced = new HashSet<string>(StringComparer.Ordinal);
        foreach (var workflow in document.Workflows)
        {
            CollectReferences(workflow.SuccessActions, referenced);
            CollectReferences(workflow.FailureActions, referenced);
            CollectReferences(workflow.Parameters, referenced);

            foreach (var step in workflow.Steps)
            {
                CollectReferences(step.Parameters, referenced);
                CollectReferences(step.OnSuccess, referenced);
                CollectReferences(step.OnFailure, referenced);
            }
        }

        CheckUnused(document.Components.Parameters?.Keys, "parameters", referenced, findings);
        CheckUnused(document.Components.SuccessActions?.Keys, "successActions", referenced, findings);
        CheckUnused(document.Components.FailureActions?.Keys, "failureActions", referenced, findings);
    }

    private static void CollectReferences<T>(IList<ArazzoReferenceable<T>>? items, HashSet<string> referenced)
        where T : IArazzoSerializable
    {
        if (items is null)
        {
            return;
        }

        foreach (var item in items)
        {
            if (item.Reference is not null)
            {
                referenced.Add(item.Reference.Reference);
            }
        }
    }

    private static void CheckUnused(IEnumerable<string>? names, string section, HashSet<string> referenced,
        List<ArazzoError> findings)
    {
        if (names is null)
        {
            return;
        }

        foreach (var name in names)
        {
            var reference = $"$components.{section}.{name}";
            if (!referenced.Contains(reference))
            {
                findings.Add(new ArazzoError($"/components/{section}/{name}",
                    $"Component '{name}' in components/{section} is never referenced.", IsWarning: true));
            }
        }
    }

    /// <summary>Depth-first cycle search over a <c>dependsOn</c> adjacency map; returns one offending path if a cycle exists.</summary>
    private static List<string>? FindCycle(Dictionary<string, List<string>> graph)
    {
        var state = new Dictionary<string, int>(StringComparer.Ordinal);
        var path = new List<string>();

        bool Visit(string node)
        {
            state[node] = 1;
            path.Add(node);

            foreach (var next in graph.GetValueOrDefault(node) ?? [])
            {
                var nextState = state.GetValueOrDefault(next);
                if (nextState == 1)
                {
                    path.Add(next);
                    return true;
                }

                if (nextState == 0 && Visit(next))
                {
                    return true;
                }
            }

            state[node] = 2;
            path.RemoveAt(path.Count - 1);
            return false;
        }

        foreach (var node in graph.Keys)
        {
            if (state.GetValueOrDefault(node) == 0 && Visit(node))
            {
                return path;
            }
        }

        return null;
    }
}
