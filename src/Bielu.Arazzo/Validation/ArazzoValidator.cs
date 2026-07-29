using Bielu.Arazzo.Models;
using Json.Schema;

namespace Bielu.Arazzo.Validation;

/// <summary>
/// Structural invariants a well-formed Arazzo document must satisfy, beyond what the type system already
/// enforces (required fields). Reference resolution against actual source documents
/// (does <c>operationId</c> exist? is <c>stepId</c> in <c>dependsOn</c> real?) needs an
/// <see cref="ArazzoWorkspace"/>, and is not this validator's concern.
/// </summary>
public static class ArazzoValidator
{
    /// <summary>Validates the structural invariants of an Arazzo document.</summary>
    /// <param name="document">The document to validate.</param>
    /// <returns>The validation errors and warnings found in the document.</returns>
    public static IReadOnlyList<ArazzoError> Validate(ArazzoDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

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

            ValidateReferenceableList(workflow.SuccessActions, $"{workflowPath}/successActions", errors, ValidateSuccessAction);
            ValidateReferenceableList(workflow.FailureActions, $"{workflowPath}/failureActions", errors, ValidateFailureAction);
            ValidateReferenceableList(workflow.Parameters, $"{workflowPath}/parameters", errors, ValidateParameter);

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

            if (step.SuccessCriteria is not null)
            {
                for (var i = 0; i < step.SuccessCriteria.Count; i++)
                {
                    ValidateCriterion(step.SuccessCriteria[i], $"{stepPath}/successCriteria/{i}", errors);
                }
            }

            ValidateReferenceableList(step.Parameters, $"{stepPath}/parameters", errors, ValidateParameter);
            ValidateReferenceableList(step.OnSuccess, $"{stepPath}/onSuccess", errors, ValidateSuccessAction);
            ValidateReferenceableList(step.OnFailure, $"{stepPath}/onFailure", errors, ValidateFailureAction);
        }
    }

    private static void ValidateReferenceableList<T>(IList<ArazzoReferenceable<T>>? items, string path, List<ArazzoError> errors, Action<T, string, List<ArazzoError>> validateItem)
        where T : IArazzoSerializable
    {
        if (items is null)
        {
            return;
        }

        for (var i = 0; i < items.Count; i++)
        {
            if (items[i].Value is { } value)
            {
                validateItem(value, $"{path}/{i}", errors);
            }
        }
    }

    private static void ValidateSuccessAction(ArazzoSuccessAction action, string path, List<ArazzoError> errors)
    {
        if (action.Type != ArazzoSuccessActionType.End && action.Type != ArazzoSuccessActionType.Goto)
        {
            errors.Add(new ArazzoError($"{path}/type", $"Unknown success action type '{action.Type}'; expected 'end' or 'goto'."));
        }

        if (action.WorkflowId is not null && action.StepId is not null)
        {
            errors.Add(new ArazzoError(path, "workflowId and stepId are mutually exclusive."));
        }
        else if (action.Type == ArazzoSuccessActionType.Goto && action.WorkflowId is null && action.StepId is null)
        {
            errors.Add(new ArazzoError(path, "A 'goto' success action requires either workflowId or stepId."));
        }

        if (action.Criteria is null)
        {
            return;
        }

        for (var i = 0; i < action.Criteria.Count; i++)
        {
            ValidateCriterion(action.Criteria[i], $"{path}/criteria/{i}", errors);
        }
    }

    private static void ValidateFailureAction(ArazzoFailureAction action, string path, List<ArazzoError> errors)
    {
        if (action.Type != ArazzoFailureActionType.End && action.Type != ArazzoFailureActionType.Retry && action.Type != ArazzoFailureActionType.Goto)
        {
            errors.Add(new ArazzoError($"{path}/type", $"Unknown failure action type '{action.Type}'; expected 'end', 'retry', or 'goto'."));
        }

        if (action.WorkflowId is not null && action.StepId is not null)
        {
            errors.Add(new ArazzoError(path, "workflowId and stepId are mutually exclusive."));
        }
        else if ((action.Type == ArazzoFailureActionType.Goto || action.Type == ArazzoFailureActionType.Retry) && action.WorkflowId is null && action.StepId is null)
        {
            errors.Add(new ArazzoError(path, $"A '{action.Type}' failure action requires either workflowId or stepId."));
        }

        if (action.Type != ArazzoFailureActionType.Retry && (action.RetryAfter is not null || action.RetryLimit is not null))
        {
            errors.Add(new ArazzoError(path, "retryAfter and retryLimit only apply when type is 'retry'."));
        }

        ValidateReferenceableList(action.Parameters, $"{path}/parameters", errors, ValidateParameter);

        if (action.Criteria is null)
        {
            return;
        }

        for (var i = 0; i < action.Criteria.Count; i++)
        {
            ValidateCriterion(action.Criteria[i], $"{path}/criteria/{i}", errors);
        }
    }

    private static void ValidateParameter(ArazzoParameter parameter, string path, List<ArazzoError> errors)
    {
        if (parameter.In is null)
        {
            return;
        }

        if (parameter.In != ArazzoParameterLocation.Path
            && parameter.In != ArazzoParameterLocation.Query
            && parameter.In != ArazzoParameterLocation.QueryString
            && parameter.In != ArazzoParameterLocation.Header
            && parameter.In != ArazzoParameterLocation.Cookie)
        {
            errors.Add(new ArazzoError($"{path}/in", $"Unknown parameter location '{parameter.In}'."));
        }
    }

    private static void ValidateCriterion(ArazzoCriterion criterion, string path, List<ArazzoError> errors)
    {
        var isSimple = criterion.Type is null || criterion.Type.Type == ArazzoCriterionType.Simple;
        if (!isSimple && criterion.Context is null)
        {
            errors.Add(new ArazzoError($"{path}/context", "context is required when type is not 'simple'."));
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
