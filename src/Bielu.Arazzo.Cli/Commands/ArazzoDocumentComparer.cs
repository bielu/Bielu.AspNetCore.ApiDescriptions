// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Bielu.Arazzo.Models;
using Bielu.Cli.Shared.Diff;

namespace Bielu.Arazzo.Cli.Commands;

internal sealed class ArazzoDocumentComparer
{
    public IEnumerable<DocumentChange> Compare(ArazzoDocument @base, ArazzoDocument head)
    {
        var changes = new List<DocumentChange>();

        CompareSourceDescriptions(@base.SourceDescriptions, head.SourceDescriptions, changes);
        CompareWorkflows(@base.Workflows, head.Workflows, changes);

        return changes;
    }

    private static void CompareSourceDescriptions(IList<ArazzoSourceDescription> @base,
        IList<ArazzoSourceDescription> head, List<DocumentChange> changes)
    {
        var baseByName = IndexBy(@base, s => s.Name);
        var headByName = IndexBy(head, s => s.Name);

        foreach (var (name, source) in baseByName)
        {
            var path = $"sourceDescriptions/{name}";

            if (!headByName.TryGetValue(name, out var headSource))
            {
                changes.Add(new DocumentChange(path, $"Source description '{name}' was removed.", ChangeSeverity.Breaking));
                continue;
            }

            if (source.Url != headSource.Url)
            {
                changes.Add(new DocumentChange($"{path}/url",
                    $"Source description '{name}' url changed from '{source.Url}' to '{headSource.Url}'.", ChangeSeverity.Breaking));
            }

            if (source.Type != headSource.Type)
            {
                changes.Add(new DocumentChange($"{path}/type",
                    $"Source description '{name}' type changed from '{source.Type}' to '{headSource.Type}'.", ChangeSeverity.Breaking));
            }
        }

        foreach (var name in headByName.Keys)
        {
            if (!baseByName.ContainsKey(name))
            {
                changes.Add(new DocumentChange($"sourceDescriptions/{name}", $"Source description '{name}' was added.",
                    ChangeSeverity.NonBreaking));
            }
        }
    }

    private static void CompareWorkflows(IList<ArazzoWorkflow> @base, IList<ArazzoWorkflow> head,
        List<DocumentChange> changes)
    {
        var baseById = IndexBy(@base, w => w.WorkflowId);
        var headById = IndexBy(head, w => w.WorkflowId);

        foreach (var (id, workflow) in baseById)
        {
            var path = $"workflows/{id}";

            if (!headById.TryGetValue(id, out var headWorkflow))
            {
                changes.Add(new DocumentChange(path, $"Workflow '{id}' was removed.", ChangeSeverity.Breaking));
                continue;
            }

            CompareSteps(id, workflow.Steps, headWorkflow.Steps, changes);
        }

        foreach (var id in headById.Keys)
        {
            if (!baseById.ContainsKey(id))
            {
                changes.Add(new DocumentChange($"workflows/{id}", $"Workflow '{id}' was added.", ChangeSeverity.NonBreaking));
            }
        }
    }

    private static void CompareSteps(string workflowId, IList<ArazzoStep> @base, IList<ArazzoStep> head,
        List<DocumentChange> changes)
    {
        var baseById = IndexBy(@base, s => s.StepId);
        var headById = IndexBy(head, s => s.StepId);

        foreach (var (id, step) in baseById)
        {
            var path = $"workflows/{workflowId}/steps/{id}";

            if (!headById.TryGetValue(id, out var headStep))
            {
                changes.Add(new DocumentChange(path, $"Step '{id}' was removed from workflow '{workflowId}'.", ChangeSeverity.Breaking));
                continue;
            }

            var baseTarget = StepTarget(step);
            var headTarget = StepTarget(headStep);
            if (baseTarget != headTarget)
            {
                changes.Add(new DocumentChange(path, $"Step '{id}' target changed from '{baseTarget}' to '{headTarget}'.",
                    ChangeSeverity.Breaking));
            }

            if (step.Action != headStep.Action)
            {
                changes.Add(new DocumentChange($"{path}/action",
                    $"Step '{id}' action changed from '{step.Action}' to '{headStep.Action}'.", ChangeSeverity.Breaking));
            }
        }

        foreach (var id in headById.Keys)
        {
            if (!baseById.ContainsKey(id))
            {
                changes.Add(new DocumentChange($"workflows/{workflowId}/steps/{id}",
                    $"Step '{id}' was added to workflow '{workflowId}'.", ChangeSeverity.NonBreaking));
            }
        }
    }

    private static string StepTarget(ArazzoStep step) =>
        step.OperationId ?? step.OperationPath ?? step.ChannelPath ?? step.WorkflowId ?? string.Empty;

    /// <summary>Indexes by key, last one wins on a duplicate — diffing tolerates malformed input that <c>validate</c> would reject.</summary>
    private static Dictionary<string, T> IndexBy<T>(IEnumerable<T> items, Func<T, string> keySelector)
    {
        var map = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            map[keySelector(item)] = item;
        }

        return map;
    }
}
