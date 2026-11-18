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
    private static readonly System.Text.RegularExpressions.Regex OutputKeyPattern = new(@"^[a-zA-Z0-9\.\-_]+$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    /// <summary>Validates the structural invariants of an Arazzo document.</summary>
    /// <param name="document">The document to validate.</param>
    /// <returns>The validation errors and warnings found in the document.</returns>
    public static IReadOnlyList<ArazzoError> Validate(ArazzoDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var errors = new List<ArazzoError>();

        ValidateSourceDescriptions(document, errors);
        ValidateWorkflows(document, errors);
        ValidateValues(document, errors);
        ValidateSelf(document, errors);
        ValidateVersion10Features(document, errors);
        ValidateComponents(document, errors);

        return errors;
    }

    /// <summary>
    /// Rejects, in a document declaring Arazzo 1.0, the fields and values Arazzo 1.1.0 added besides <c>$self</c>,
    /// the Selector Object and <c>targetSelectorType</c> (checked elsewhere): AsyncAPI sources and their step fields,
    /// step <c>timeout</c> and <c>dependsOn</c>, <c>parameters</c> on success and failure actions,
    /// the <c>querystring</c> parameter location, and the
    /// <c>jsonpointer</c> criterion type and <c>rfc9535</c> JSONPath version.
    /// </summary>
    private static void ValidateVersion10Features(ArazzoDocument document, List<ArazzoError> errors)
    {
        if (!ArazzoVersionExtensions.TryParse(document.Arazzo, out var version) || version != ArazzoVersion.V1_0)
        {
            return;
        }

        void Require11(string path, string feature) => errors.Add(new ArazzoError(path, $"{feature} requires Arazzo 1.1.0 or later."));

        void CheckParameters(IList<ArazzoReferenceable<ArazzoParameter>>? parameters, string path)
        {
            for (var i = 0; parameters is not null && i < parameters.Count; i++)
            {
                if (parameters[i].Value?.In == ArazzoParameterLocation.QueryString)
                {
                    Require11($"{path}/{i}/in", "The 'querystring' parameter location");
                }
            }
        }

        void CheckCriteria(IList<ArazzoCriterion>? criteria, string path)
        {
            for (var i = 0; criteria is not null && i < criteria.Count; i++)
            {
                if (criteria[i].Type is not { } type)
                {
                    continue;
                }

                if (type.Type == "jsonpointer")
                {
                    Require11($"{path}/{i}/type", "The 'jsonpointer' criterion type");
                }

                if (type.Version == "rfc9535")
                {
                    Require11($"{path}/{i}/type/version", "The 'rfc9535' JSONPath version");
                }
            }
        }

        void CheckActionParameters(IList<ArazzoReferenceable<ArazzoParameter>>? parameters, string path)
        {
            if (parameters is not null)
            {
                Require11(path, "parameters on a success or failure action");
            }
        }

        void CheckSuccessAction(ArazzoSuccessAction? action, string path)
        {
            CheckCriteria(action?.Criteria, $"{path}/criteria");
            CheckActionParameters(action?.Parameters, $"{path}/parameters");
        }

        void CheckFailureAction(ArazzoFailureAction? action, string path)
        {
            CheckCriteria(action?.Criteria, $"{path}/criteria");
            CheckActionParameters(action?.Parameters, $"{path}/parameters");
        }

        void CheckSuccessActions(IList<ArazzoReferenceable<ArazzoSuccessAction>>? actions, string path)
        {
            for (var i = 0; actions is not null && i < actions.Count; i++)
            {
                CheckSuccessAction(actions[i].Value, $"{path}/{i}");
            }
        }

        void CheckFailureActions(IList<ArazzoReferenceable<ArazzoFailureAction>>? actions, string path)
        {
            for (var i = 0; actions is not null && i < actions.Count; i++)
            {
                CheckFailureAction(actions[i].Value, $"{path}/{i}");
            }
        }

        for (var i = 0; i < document.SourceDescriptions.Count; i++)
        {
            if (document.SourceDescriptions[i].Type == ArazzoSourceDescriptionType.AsyncApi)
            {
                Require11($"/sourceDescriptions/{i}/type", "An 'asyncapi' source description");
            }
        }

        if (document.Components is { } components)
        {
            foreach (var (name, parameter) in components.Parameters ?? new Dictionary<string, ArazzoParameter>())
            {
                if (parameter.In == ArazzoParameterLocation.QueryString)
                {
                    Require11($"/components/parameters/{EscapePointerSegment(name)}/in", "The 'querystring' parameter location");
                }
            }

            foreach (var (name, action) in components.SuccessActions ?? new Dictionary<string, ArazzoSuccessAction>())
            {
                CheckSuccessAction(action, $"/components/successActions/{EscapePointerSegment(name)}");
            }

            foreach (var (name, action) in components.FailureActions ?? new Dictionary<string, ArazzoFailureAction>())
            {
                CheckFailureAction(action, $"/components/failureActions/{EscapePointerSegment(name)}");
            }
        }

        for (var w = 0; w < document.Workflows.Count; w++)
        {
            var workflow = document.Workflows[w];
            var workflowPath = $"/workflows/{w}";
            CheckParameters(workflow.Parameters, $"{workflowPath}/parameters");
            CheckSuccessActions(workflow.SuccessActions, $"{workflowPath}/successActions");
            CheckFailureActions(workflow.FailureActions, $"{workflowPath}/failureActions");

            for (var s = 0; s < workflow.Steps.Count; s++)
            {
                var step = workflow.Steps[s];
                var stepPath = $"{workflowPath}/steps/{s}";
                if (step.ChannelPath is not null)
                {
                    Require11($"{stepPath}/channelPath", "channelPath");
                }

                if (step.Action is not null)
                {
                    Require11($"{stepPath}/action", "action");
                }

                if (step.CorrelationId is not null)
                {
                    Require11($"{stepPath}/correlationId", "correlationId");
                }

                if (step.Timeout is not null)
                {
                    Require11($"{stepPath}/timeout", "A step timeout");
                }

                if (step.DependsOn is not null)
                {
                    Require11($"{stepPath}/dependsOn", "A step dependsOn");
                }

                CheckParameters(step.Parameters, $"{stepPath}/parameters");
                CheckCriteria(step.SuccessCriteria, $"{stepPath}/successCriteria");
                CheckSuccessActions(step.OnSuccess, $"{stepPath}/onSuccess");
                CheckFailureActions(step.OnFailure, $"{stepPath}/onFailure");
            }
        }
    }

    private static void ValidateSelf(ArazzoDocument document, List<ArazzoError> errors)
    {
        if (document.Self is null)
        {
            return;
        }

        if (ArazzoVersionExtensions.TryParse(document.Arazzo, out var version) && version == ArazzoVersion.V1_0)
        {
            errors.Add(new ArazzoError("/$self", "$self requires Arazzo 1.1.0 or later."));
        }

        if (document.Self.Contains('#', StringComparison.Ordinal))
        {
            errors.Add(new ArazzoError("/$self", "$self MUST NOT contain a fragment."));
        }
    }

    /// <summary>
    /// Validates the reusable objects in <c>components</c> like their inline counterparts, and checks that every
    /// Reusable Object (<c>$components.{kind}.{name}</c>) and <c>#/components/inputs/{name}</c> schema reference
    /// in the document points at an entry of the right kind that exists.
    /// </summary>
    private static void ValidateComponents(ArazzoDocument document, List<ArazzoError> errors)
    {
        var components = document.Components;
        foreach (var (name, parameter) in components?.Parameters ?? new Dictionary<string, ArazzoParameter>())
        {
            ValidateParameter(parameter, $"/components/parameters/{EscapePointerSegment(name)}", errors);
        }

        foreach (var (name, action) in components?.SuccessActions ?? new Dictionary<string, ArazzoSuccessAction>())
        {
            var path = $"/components/successActions/{EscapePointerSegment(name)}";
            ValidateSuccessAction(action, path, errors);
            CheckReferences(action.Parameters, "parameters", components?.Parameters, $"{path}/parameters", errors);
        }

        foreach (var (name, action) in components?.FailureActions ?? new Dictionary<string, ArazzoFailureAction>())
        {
            var path = $"/components/failureActions/{EscapePointerSegment(name)}";
            ValidateFailureAction(action, path, errors);
            CheckReferences(action.Parameters, "parameters", components?.Parameters, $"{path}/parameters", errors);
        }

        for (var w = 0; w < document.Workflows.Count; w++)
        {
            var workflow = document.Workflows[w];
            var workflowPath = $"/workflows/{w}";
            CheckInputsReference(workflow.Inputs, components?.Inputs, $"{workflowPath}/inputs", errors);
            CheckReferences(workflow.Parameters, "parameters", components?.Parameters, $"{workflowPath}/parameters", errors);
            CheckReferences(workflow.SuccessActions, "successActions", components?.SuccessActions, $"{workflowPath}/successActions", errors);
            CheckReferences(workflow.FailureActions, "failureActions", components?.FailureActions, $"{workflowPath}/failureActions", errors);
            CheckNestedParameterReferences(workflow.SuccessActions, a => a.Parameters, components?.Parameters, $"{workflowPath}/successActions", errors);
            CheckNestedParameterReferences(workflow.FailureActions, a => a.Parameters, components?.Parameters, $"{workflowPath}/failureActions", errors);

            for (var s = 0; s < workflow.Steps.Count; s++)
            {
                var step = workflow.Steps[s];
                var stepPath = $"{workflowPath}/steps/{s}";
                CheckReferences(step.Parameters, "parameters", components?.Parameters, $"{stepPath}/parameters", errors);
                CheckReferences(step.OnSuccess, "successActions", components?.SuccessActions, $"{stepPath}/onSuccess", errors);
                CheckReferences(step.OnFailure, "failureActions", components?.FailureActions, $"{stepPath}/onFailure", errors);
                CheckNestedParameterReferences(step.OnSuccess, a => a.Parameters, components?.Parameters, $"{stepPath}/onSuccess", errors);
                CheckNestedParameterReferences(step.OnFailure, a => a.Parameters, components?.Parameters, $"{stepPath}/onFailure", errors);
            }
        }
    }

    private static void CheckNestedParameterReferences<TAction>(IList<ArazzoReferenceable<TAction>>? actions,
        Func<TAction, IList<ArazzoReferenceable<ArazzoParameter>>?> actionParameters,
        IDictionary<string, ArazzoParameter>? parameters, string path, List<ArazzoError> errors)
        where TAction : class, IArazzoSerializable
    {
        for (var i = 0; actions is not null && i < actions.Count; i++)
        {
            if (actions[i].Value is { } action)
            {
                CheckReferences(actionParameters(action), "parameters", parameters, $"{path}/{i}/parameters", errors);
            }
        }
    }

    private static void CheckReferences<T, TComponent>(IList<ArazzoReferenceable<T>>? items, string kind,
        IDictionary<string, TComponent>? components, string path, List<ArazzoError> errors)
        where T : IArazzoSerializable
    {
        var prefix = $"$components.{kind}.";
        for (var i = 0; items is not null && i < items.Count; i++)
        {
            if (items[i].Reference is not { } reference)
            {
                continue;
            }

            var referencePath = $"{path}/{i}/reference";
            if (!reference.Reference.StartsWith(prefix, StringComparison.Ordinal))
            {
                errors.Add(new ArazzoError(referencePath, $"'{reference.Reference}' must reference components.{kind} ('{prefix}NAME') in this position."));
                continue;
            }

            var name = reference.Reference[prefix.Length..];
            if (components is null || !components.ContainsKey(name))
            {
                errors.Add(new ArazzoError(referencePath, $"'{reference.Reference}' does not resolve; components.{kind} has no '{name}'."));
            }

            if (reference.Value is not null && kind != "parameters")
            {
                errors.Add(new ArazzoError($"{path}/{i}/value", "value only applies to a reference to a parameter."));
            }
        }
    }

    private static void CheckInputsReference(System.Text.Json.Nodes.JsonNode? inputs, IDictionary<string, System.Text.Json.Nodes.JsonNode?>? components,
        string path, List<ArazzoError> errors)
    {
        const string Prefix = "#/components/inputs/";
        if (inputs is not System.Text.Json.Nodes.JsonObject schema
            || !schema.TryGetPropertyValue("$ref", out var refNode)
            || refNode is not System.Text.Json.Nodes.JsonValue refValue
            || !refValue.TryGetValue<string>(out var reference)
            || !reference.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return;
        }

        var name = reference[Prefix.Length..];
        if (components is null || !components.ContainsKey(name))
        {
            errors.Add(new ArazzoError($"{path}/$ref", $"'{reference}' does not resolve; components.inputs has no '{name}'."));
        }
    }

    /// <summary>
    /// Checks every <c>Any | {expression} | Selector Object</c> position: outputs may not be literals, and a
    /// document declaring Arazzo 1.0 may not use the Selector Object or <c>targetSelectorType</c>, both new in 1.1.0.
    /// </summary>
    private static void ValidateValues(ArazzoDocument document, List<ArazzoError> errors)
    {
        var isVersion10 = ArazzoVersionExtensions.TryParse(document.Arazzo, out var version) && version == ArazzoVersion.V1_0;

        void CheckValue(ArazzoValue? value, string path)
        {
            if (value is { IsSelector: true } && isVersion10)
            {
                errors.Add(new ArazzoError(path, "Selector Objects require Arazzo 1.1.0 or later."));
            }
        }

        void CheckOutputs(IDictionary<string, ArazzoValue>? outputs, string path)
        {
            if (outputs is null)
            {
                return;
            }

            foreach (var (name, value) in outputs)
            {
                var outputPath = $"{path}/{EscapePointerSegment(name)}";
                if (!OutputKeyPattern.IsMatch(name))
                {
                    errors.Add(new ArazzoError(outputPath, $@"Output name '{name}' must match ^[a-zA-Z0-9\.\-_]+$."));
                }

                if (value.IsLiteral)
                {
                    errors.Add(new ArazzoError(outputPath, "An output must be a runtime expression or a Selector Object, not a literal value."));
                }

                CheckValue(value, outputPath);
            }
        }

        void CheckParameters(IList<ArazzoReferenceable<ArazzoParameter>>? parameters, string path)
        {
            for (var i = 0; parameters is not null && i < parameters.Count; i++)
            {
                CheckValue(parameters[i].Value?.Value, $"{path}/{i}/value");
            }
        }

        void CheckFailureActions(IList<ArazzoReferenceable<ArazzoFailureAction>>? actions, string path)
        {
            for (var i = 0; actions is not null && i < actions.Count; i++)
            {
                CheckParameters(actions[i].Value?.Parameters, $"{path}/{i}/parameters");
            }
        }

        if (document.Components is { } components)
        {
            foreach (var (name, parameter) in components.Parameters ?? new Dictionary<string, ArazzoParameter>())
            {
                CheckValue(parameter.Value, $"/components/parameters/{EscapePointerSegment(name)}/value");
            }

            foreach (var (name, action) in components.FailureActions ?? new Dictionary<string, ArazzoFailureAction>())
            {
                for (var i = 0; action.Parameters is not null && i < action.Parameters.Count; i++)
                {
                    CheckValue(action.Parameters[i].Value?.Value, $"/components/failureActions/{EscapePointerSegment(name)}/parameters/{i}/value");
                }
            }
        }

        for (var w = 0; w < document.Workflows.Count; w++)
        {
            var workflow = document.Workflows[w];
            var workflowPath = $"/workflows/{w}";
            CheckParameters(workflow.Parameters, $"{workflowPath}/parameters");
            CheckFailureActions(workflow.FailureActions, $"{workflowPath}/failureActions");
            CheckOutputs(workflow.Outputs, $"{workflowPath}/outputs");

            for (var s = 0; s < workflow.Steps.Count; s++)
            {
                var step = workflow.Steps[s];
                var stepPath = $"{workflowPath}/steps/{s}";
                CheckParameters(step.Parameters, $"{stepPath}/parameters");
                CheckFailureActions(step.OnFailure, $"{stepPath}/onFailure");
                CheckOutputs(step.Outputs, $"{stepPath}/outputs");

                if (step.RequestBody is not { } body)
                {
                    continue;
                }

                CheckValue(body.Payload, $"{stepPath}/requestBody/payload");
                for (var r = 0; body.Replacements is not null && r < body.Replacements.Count; r++)
                {
                    var replacement = body.Replacements[r];
                    var replacementPath = $"{stepPath}/requestBody/replacements/{r}";
                    if (replacement.TargetSelectorType is not null && isVersion10)
                    {
                        errors.Add(new ArazzoError($"{replacementPath}/targetSelectorType", "targetSelectorType requires Arazzo 1.1.0 or later."));
                    }

                    CheckValue(replacement.Value, $"{replacementPath}/value");
                }
            }
        }
    }

    private static string EscapePointerSegment(string segment) => segment.Replace("~", "~0").Replace("/", "~1");

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

        ValidateReferenceableList(action.Parameters, $"{path}/parameters", errors, ValidateActionParameter);

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

        ValidateReferenceableList(action.Parameters, $"{path}/parameters", errors, ValidateActionParameter);

        if (action.Criteria is null)
        {
            return;
        }

        for (var i = 0; i < action.Criteria.Count; i++)
        {
            ValidateCriterion(action.Criteria[i], $"{path}/criteria/{i}", errors);
        }
    }

    /// <summary>A success or failure action passes its parameters to a workflow, so the spec forbids their <c>in</c>.</summary>
    private static void ValidateActionParameter(ArazzoParameter parameter, string path, List<ArazzoError> errors)
    {
        if (parameter.In is not null)
        {
            errors.Add(new ArazzoError($"{path}/in", "in MUST NOT be used on a success or failure action parameter; its parameters go to a workflow."));
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
