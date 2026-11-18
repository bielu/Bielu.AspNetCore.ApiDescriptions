using System.Text.Json.Nodes;
using Bielu.Arazzo.Models;

namespace Bielu.AspNetCore.Arazzo.Services;

/// <summary>
/// Deep copies of the Arazzo object model, so each document build works on its own instances. Document
/// transformers mutate the document in place. If a build fails or is cancelled after a transformer changed it,
/// the change must not stay in the configured <see cref="ArazzoOptions"/>, or the next attempt would start from
/// the half-transformed model (a step appended twice, for example).
/// </summary>
internal static class ArazzoModelCopy
{
    public static ArazzoInfo Copy(ArazzoInfo info) => new()
    {
        Title = info.Title,
        Summary = info.Summary,
        Description = info.Description,
        Version = info.Version,
        Extensions = CopyExtensions(info.Extensions),
    };

    public static ArazzoSourceDescription Copy(ArazzoSourceDescription source) => new()
    {
        Name = source.Name,
        Url = source.Url,
        Type = source.Type,
        Extensions = CopyExtensions(source.Extensions),
    };

    public static ArazzoWorkflow Copy(ArazzoWorkflow workflow) => new()
    {
        WorkflowId = workflow.WorkflowId,
        Summary = workflow.Summary,
        Description = workflow.Description,
        Inputs = workflow.Inputs?.DeepClone(),
        DependsOn = CopyStrings(workflow.DependsOn),
        Steps = [.. workflow.Steps.Select(Copy)],
        SuccessActions = CopyReferenceables(workflow.SuccessActions, Copy),
        FailureActions = CopyReferenceables(workflow.FailureActions, Copy),
        Outputs = CopyMap(workflow.Outputs, Copy),
        Parameters = CopyReferenceables(workflow.Parameters, Copy),
        Extensions = CopyExtensions(workflow.Extensions),
    };

    public static ArazzoComponents Copy(ArazzoComponents components) => new()
    {
        Inputs = CopyExtensions(components.Inputs),
        Parameters = CopyMap(components.Parameters, Copy),
        SuccessActions = CopyMap(components.SuccessActions, Copy),
        FailureActions = CopyMap(components.FailureActions, Copy),
        Extensions = CopyExtensions(components.Extensions),
    };

    public static Dictionary<string, JsonNode?>? CopyExtensions(IDictionary<string, JsonNode?>? extensions) =>
        CopyMap(extensions, node => node?.DeepClone());

    private static ArazzoStep Copy(ArazzoStep step) => new()
    {
        Description = step.Description,
        StepId = step.StepId,
        OperationId = step.OperationId,
        OperationPath = step.OperationPath,
        ChannelPath = step.ChannelPath,
        WorkflowId = step.WorkflowId,
        Parameters = CopyReferenceables(step.Parameters, Copy),
        RequestBody = step.RequestBody is { } body ? Copy(body) : null,
        SuccessCriteria = CopyList(step.SuccessCriteria, Copy),
        OnSuccess = CopyReferenceables(step.OnSuccess, Copy),
        OnFailure = CopyReferenceables(step.OnFailure, Copy),
        Outputs = CopyMap(step.Outputs, Copy),
        Timeout = step.Timeout,
        CorrelationId = step.CorrelationId,
        Action = step.Action,
        DependsOn = CopyStrings(step.DependsOn),
        Extensions = CopyExtensions(step.Extensions),
    };

    private static ArazzoRequestBody Copy(ArazzoRequestBody body) => new()
    {
        ContentType = body.ContentType,
        Payload = body.Payload is { } payload ? Copy(payload) : null,
        Replacements = CopyList(body.Replacements, Copy),
        Extensions = CopyExtensions(body.Extensions),
    };

    private static ArazzoPayloadReplacement Copy(ArazzoPayloadReplacement replacement) => new()
    {
        Target = replacement.Target,
        TargetSelectorType = replacement.TargetSelectorType is { } type ? Copy(type) : null,
        Value = Copy(replacement.Value),
        Extensions = CopyExtensions(replacement.Extensions),
    };

    private static ArazzoParameter Copy(ArazzoParameter parameter) => new()
    {
        Name = parameter.Name,
        In = parameter.In,
        Value = Copy(parameter.Value),
        Extensions = CopyExtensions(parameter.Extensions),
    };

    private static ArazzoSuccessAction Copy(ArazzoSuccessAction action) => new()
    {
        Name = action.Name,
        Type = action.Type,
        WorkflowId = action.WorkflowId,
        StepId = action.StepId,
        Parameters = CopyReferenceables(action.Parameters, Copy),
        Criteria = CopyList(action.Criteria, Copy),
        Extensions = CopyExtensions(action.Extensions),
    };

    private static ArazzoFailureAction Copy(ArazzoFailureAction action) => new()
    {
        Name = action.Name,
        Type = action.Type,
        WorkflowId = action.WorkflowId,
        StepId = action.StepId,
        Parameters = CopyReferenceables(action.Parameters, Copy),
        RetryAfter = action.RetryAfter,
        RetryLimit = action.RetryLimit,
        Criteria = CopyList(action.Criteria, Copy),
        Extensions = CopyExtensions(action.Extensions),
    };

    private static ArazzoCriterion Copy(ArazzoCriterion criterion) => new()
    {
        Context = criterion.Context,
        Condition = criterion.Condition,
        Type = criterion.Type is { } type ? Copy(type) : null,
        Extensions = CopyExtensions(criterion.Extensions),
    };

    private static ArazzoSelectorType Copy(ArazzoSelectorType type) => new() { Type = type.Type, Version = type.Version };

    private static ArazzoSelector Copy(ArazzoSelector selector) => new()
    {
        Context = selector.Context,
        Selector = selector.Selector,
        Type = Copy(selector.Type),
        Extensions = CopyExtensions(selector.Extensions),
    };

    // An ArazzoValue cannot be changed once made, but its literal JSON and its selector can.
    private static ArazzoValue Copy(ArazzoValue value) => value.Kind switch
    {
        ArazzoValueKind.Literal => ArazzoValue.FromLiteral(value.Literal?.DeepClone()),
        ArazzoValueKind.Selector => ArazzoValue.FromSelector(Copy(value.Selector!)),
        _ => value,
    };

    private static ArazzoReusableObject Copy(ArazzoReusableObject reference) => new() { Reference = reference.Reference, Value = reference.Value };

    private static List<ArazzoReferenceable<T>>? CopyReferenceables<T>(IList<ArazzoReferenceable<T>>? items, Func<T, T> copy)
        where T : class, IArazzoSerializable =>
        CopyList(items, item => new ArazzoReferenceable<T>
        {
            Value = item.Value is { } value ? copy(value) : null,
            Reference = item.Reference is { } reference ? Copy(reference) : null,
        });

    private static List<string>? CopyStrings(IList<string>? items) => items is null ? null : [.. items];

    private static List<T>? CopyList<T>(IList<T>? items, Func<T, T> copy) => items?.Select(copy).ToList();

    // Ordinal, as every map the options build is.
    private static Dictionary<string, T>? CopyMap<T>(IDictionary<string, T>? map, Func<T, T> copy) =>
        map?.ToDictionary(pair => pair.Key, pair => copy(pair.Value), StringComparer.Ordinal);
}
