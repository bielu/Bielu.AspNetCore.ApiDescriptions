using Bielu.Arazzo.Models;
using Json.Schema;

namespace Bielu.Arazzo.Validation;

/// <summary>
/// Structural invariants a well-formed Arazzo document must satisfy, beyond what the type system already
/// enforces (required fields). Reference resolution against actual source documents
/// (does <c>operationId</c> exist? is <c>stepId</c> in <c>dependsOn</c> real?) needs an
/// <see cref="ArazzoWorkspace"/> and is PR 14/18 scope, not this validator's.
/// </summary>
public static class ArazzoValidator
{
    public static IReadOnlyList<ArazzoError> Validate(ArazzoDocument document)
    {
        var errors = new List<ArazzoError>();

        ValidateSourceDescriptions(document, errors);
        ValidateWorkflows(document, errors);

        return errors;
    }

    private static void ValidateSourceDescriptions(ArazzoDocument document, List<ArazzoError> errors)
    {
        var seenNames = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < document.SourceDescriptions.Count; i++)
        {
            var source = document.SourceDescriptions[i];
            var path = $"/sourceDescriptions/{i}";

            if (!seenNames.Add(source.Name))
            {
                errors.Add(new ArazzoError(path, $"Duplicate source description name '{source.Name}'."));
            }

            if (source.Type is not null
                && source.Type != ArazzoSourceDescriptionType.OpenApi
                && source.Type != ArazzoSourceDescriptionType.AsyncApi
                && source.Type != ArazzoSourceDescriptionType.Arazzo)
            {
                errors.Add(new ArazzoError($"{path}/type", $"Unknown source description type '{source.Type}'."));
            }
        }
    }

    private static void ValidateWorkflows(ArazzoDocument document, List<ArazzoError> errors)
    {
        var seenWorkflowIds = new HashSet<string>(StringComparer.Ordinal);
        for (var w = 0; w < document.Workflows.Count; w++)
        {
            var workflow = document.Workflows[w];
            var workflowPath = $"/workflows/{w}";

            if (!seenWorkflowIds.Add(workflow.WorkflowId))
            {
                errors.Add(new ArazzoError(workflowPath, $"Duplicate workflowId '{workflow.WorkflowId}'."));
            }

            if (workflow.Steps.Count == 0)
            {
                errors.Add(new ArazzoError($"{workflowPath}/steps", "Workflow has no steps.", IsWarning: true));
            }

            if (workflow.Inputs is not null)
            {
                ValidateSchemaShape($"{workflowPath}/inputs", workflow.Inputs, errors);
            }

            ValidateSteps(workflow, workflowPath, errors);
        }
    }

    private static void ValidateSteps(ArazzoWorkflow workflow, string workflowPath, List<ArazzoError> errors)
    {
        var seenStepIds = new HashSet<string>(StringComparer.Ordinal);
        for (var s = 0; s < workflow.Steps.Count; s++)
        {
            var step = workflow.Steps[s];
            var stepPath = $"{workflowPath}/steps/{s}";

            if (!seenStepIds.Add(step.StepId))
            {
                errors.Add(new ArazzoError(stepPath, $"Duplicate stepId '{step.StepId}' within workflow '{workflow.WorkflowId}'."));
            }

            var targetCount = new[] { step.OperationId, step.OperationPath, step.ChannelPath, step.WorkflowId }
                .Count(t => t is not null);
            if (targetCount == 0)
            {
                errors.Add(new ArazzoError(stepPath, "Step must set exactly one of operationId, operationPath, channelPath, or workflowId."));
            }
            else if (targetCount > 1)
            {
                errors.Add(new ArazzoError(stepPath, "Step must set exactly one of operationId, operationPath, channelPath, or workflowId; more than one was set."));
            }

            if (step.Action is not null && step.Action != ArazzoStepAction.Send && step.Action != ArazzoStepAction.Receive)
            {
                errors.Add(new ArazzoError($"{stepPath}/action", $"Unknown step action '{step.Action}'; expected 'send' or 'receive'."));
            }

            if (step.CorrelationId is not null && step.Action != ArazzoStepAction.Receive)
            {
                errors.Add(new ArazzoError($"{stepPath}/correlationId", "correlationId only applies to asyncapi steps with action 'receive'.", IsWarning: true));
            }

            if (step.ChannelPath is not null && step.Action is null)
            {
                errors.Add(new ArazzoError($"{stepPath}/channelPath", "A channelPath step SHOULD specify 'action' (send or receive).", IsWarning: true));
            }
        }
    }

    private static void ValidateSchemaShape(string path, System.Text.Json.Nodes.JsonNode schemaNode, List<ArazzoError> errors)
    {
        try
        {
            JsonSchema.FromText(schemaNode.ToJsonString());
        }
        catch (Exception ex)
        {
            errors.Add(new ArazzoError(path, $"Not a syntactically valid JSON Schema: {ex.Message}"));
        }
    }
}
