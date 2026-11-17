using System.Text.Json.Nodes;
using Bielu.Arazzo.Models;

namespace Bielu.AspNetCore.Arazzo.Services;

/// <summary>Fluent builder for a single <see cref="ArazzoStep"/> within an <see cref="ArazzoWorkflowBuilder"/>.</summary>
public sealed class ArazzoStepBuilder
{
    private readonly string _stepId;
    private readonly List<string> _dependsOn = [];
    private readonly List<ArazzoCriterion> _successCriteria = [];
    private readonly List<ArazzoReferenceable<ArazzoParameter>> _parameters = [];
    private readonly List<ArazzoReferenceable<ArazzoSuccessAction>> _onSuccess = [];
    private readonly List<ArazzoReferenceable<ArazzoFailureAction>> _onFailure = [];
    private readonly Dictionary<string, ArazzoValue> _outputs = new(StringComparer.Ordinal);
    private readonly List<ArazzoPayloadReplacement> _replacements = [];

    internal ArazzoStepBuilder(string stepId) => _stepId = stepId;

    private string? Description { get; set; }

    private string? OperationId { get; set; }

    private string? OperationPathValue { get; set; }

    private string? ChannelPathValue { get; set; }

    private string? WorkflowIdValue { get; set; }

    private string? ActionValue { get; set; }

    private string? CorrelationIdValue { get; set; }

    private ArazzoValue? PayloadValue { get; set; }

    private string? PayloadContentType { get; set; }

    private int? TimeoutValue { get; set; }

    /// <summary>Sets the step's description.</summary>
    public ArazzoStepBuilder WithDescription(string description)
    {
        Description = description;
        return this;
    }

    /// <summary>
    /// Targets an operation by its <c>operationId</c>, searched for across every source description registered
    /// on the workflow's document at startup-validation time (the spec does not require the id to be
    /// source-qualified). Mutually exclusive with <see cref="OperationPath"/>, <see cref="Channel"/>, and <see cref="Workflow"/>.
    /// </summary>
    public ArazzoStepBuilder Operation(string operationId)
    {
        ArgumentException.ThrowIfNullOrEmpty(operationId);
        EnsureNoTargetSet(nameof(Operation));
        OperationId = operationId;
        return this;
    }

    /// <summary>
    /// Targets an OpenAPI operation at <paramref name="path"/>/<paramref name="httpMethod"/> in the source
    /// description named <paramref name="sourceName"/>. Mutually exclusive with <see cref="Operation"/>,
    /// <see cref="Channel"/>, and <see cref="Workflow"/>.
    /// </summary>
    public ArazzoStepBuilder OperationPath(string sourceName, string path, string httpMethod)
    {
        ArazzoIdentifier.Validate(sourceName, nameof(sourceName));
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentException.ThrowIfNullOrEmpty(httpMethod);
        EnsureNoTargetSet(nameof(OperationPath));
        OperationPathValue =
            $"{{$sourceDescriptions.{sourceName}.url}}#/paths/{EscapePointerSegment(path)}/{httpMethod.ToLowerInvariant()}";
        return this;
    }

    /// <summary>
    /// Targets an AsyncAPI channel named <paramref name="channelName"/> in the source description named
    /// <paramref name="sourceName"/>, with an explicit send/receive <paramref name="action"/>
    /// (see <see cref="ArazzoStepAction"/>). Mutually exclusive with <see cref="Operation"/>,
    /// <see cref="OperationPath"/>, and <see cref="Workflow"/>.
    /// </summary>
    public ArazzoStepBuilder Channel(string sourceName, string channelName, string action)
    {
        ArazzoIdentifier.Validate(sourceName, nameof(sourceName));
        ArgumentException.ThrowIfNullOrEmpty(channelName);
        ArgumentException.ThrowIfNullOrEmpty(action);
        EnsureNoTargetSet(nameof(Channel));
        ChannelPathValue = $"{{$sourceDescriptions.{sourceName}.url}}#/channels/{EscapePointerSegment(channelName)}";
        ActionValue = action;
        return this;
    }

    /// <summary>Links this step's channel action to a <c>correlationId</c> defined in the AsyncAPI document. Only applicable when the step's action is "receive".</summary>
    public ArazzoStepBuilder WithCorrelationId(string correlationId)
    {
        ArgumentException.ThrowIfNullOrEmpty(correlationId);
        CorrelationIdValue = correlationId;
        return this;
    }

    /// <summary>Targets another workflow by id. Mutually exclusive with <see cref="Operation"/>, <see cref="OperationPath"/>, and <see cref="Channel"/>.</summary>
    public ArazzoStepBuilder Workflow(string workflowId)
    {
        ArazzoIdentifier.Validate(workflowId, nameof(workflowId));
        EnsureNoTargetSet(nameof(Workflow));
        WorkflowIdValue = workflowId;
        return this;
    }

    /// <summary>Throws if a target is already set, since <c>Operation</c>/<c>OperationPath</c>/<c>Channel</c>/<c>Workflow</c> are mutually exclusive — including calling the same method twice.</summary>
    private void EnsureNoTargetSet(string methodName)
    {
        if (OperationId is not null || OperationPathValue is not null || ChannelPathValue is not null ||
            WorkflowIdValue is not null)
        {
            throw new InvalidOperationException(
                $"Step '{_stepId}' already has a target set; {methodName} is mutually exclusive with Operation/OperationPath/Channel/Workflow, and only one may be called per step.");
        }
    }

    /// <summary>
    /// Targets the workflow identified by the marker type <typeparamref name="TWorkflow"/> (see
    /// <see cref="ArazzoId"/>). Mutually exclusive with <see cref="Operation"/>,
    /// <see cref="OperationPath"/>, and <see cref="Channel"/>.
    /// </summary>
    /// <typeparam name="TWorkflow">The marker type naming the workflow this step targets.</typeparam>
    public ArazzoStepBuilder Workflow<TWorkflow>() => Workflow(ArazzoId.FromType<TWorkflow>());

    /// <summary>Adds stepIds that must complete before this step executes.</summary>
    public ArazzoStepBuilder DependsOn(params string[] stepIds)
    {
        ArgumentNullException.ThrowIfNull(stepIds);
        foreach (var stepId in stepIds)
        {
            ArazzoIdentifier.Validate(stepId, nameof(stepIds));
        }

        _dependsOn.AddRange(stepIds);
        return this;
    }

    /// <summary>
    /// Adds the step identified by the marker type <typeparamref name="TStep"/> (see <see cref="ArazzoId"/>)
    /// as one that must complete before this step executes. Chain the call to depend on several steps.
    /// </summary>
    /// <typeparam name="TStep">The marker type naming the step depended on.</typeparam>
    public ArazzoStepBuilder DependsOn<TStep>() => DependsOn(ArazzoId.FromType<TStep>());

    /// <summary>
    /// Sets the step's request body payload from a literal JSON value. Calling it again, or another
    /// <c>Payload*</c> method, replaces the payload and content type; replacements added with
    /// <see cref="Replacement"/> are kept, whichever is called first.
    /// </summary>
    public ArazzoStepBuilder Payload(JsonNode payload, string? contentType = null)
    {
        ArgumentNullException.ThrowIfNull(payload);
        SetPayload(ArazzoValue.FromLiteral(payload), contentType);
        return this;
    }

    /// <summary>
    /// Sets the step's request body payload to a runtime expression, e.g. <c>$inputs.body</c>. Replacements
    /// added with <see cref="Replacement"/> are kept, whichever is called first.
    /// </summary>
    public ArazzoStepBuilder PayloadExpression(string expression, string? contentType = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(expression);
        SetPayload(ArazzoValue.FromExpression(expression), contentType);
        return this;
    }

    /// <summary>
    /// Sets the step's request body payload to the data a Selector Object picks out (Arazzo 1.1), e.g. the
    /// <c>$.order</c> part of an earlier step's response. Replacements added with <see cref="Replacement"/>
    /// are kept, whichever is called first.
    /// </summary>
    /// <example>
    /// <code>
    /// step.PayloadSelector(new ArazzoSelector
    /// {
    ///     Context = "$steps.getCart.outputs.cart",
    ///     Selector = "$.order",
    ///     Type = "jsonpath",
    /// });
    /// </code>
    /// </example>
    public ArazzoStepBuilder PayloadSelector(ArazzoSelector selector, string? contentType = null)
    {
        ArgumentNullException.ThrowIfNull(selector);
        SetPayload(ArazzoValue.FromSelector(selector), contentType);
        return this;
    }

    private void SetPayload(ArazzoValue payload, string? contentType)
    {
        PayloadValue = payload;
        PayloadContentType = contentType;
    }

    /// <summary>
    /// Adds a Payload Replacement Object: before the request is sent, the location <paramref name="target"/>
    /// picks out of the payload is set to <paramref name="value"/>. Replacements apply in the order they
    /// are added, and are independent of when the payload itself is set.
    /// </summary>
    /// <param name="target">
    /// A JSON Pointer (e.g. <c>/customer/id</c>), JSONPath or XPath expression locating the value to replace.
    /// </param>
    /// <param name="value">
    /// The value to set: a runtime expression (a plain string converts to one), a literal from
    /// <see cref="ArazzoValue.FromLiteral"/>, or a Selector Object from <see cref="ArazzoValue.FromSelector"/>
    /// (Arazzo 1.1).
    /// </param>
    /// <param name="targetSelectorType">
    /// The selector engine for <paramref name="target"/> (Arazzo 1.1). When omitted, a <c>/</c>-prefixed
    /// target is read as a JSON Pointer and any other as JSONPath.
    /// </param>
    /// <example>
    /// <code>
    /// step.Payload(JsonNode.Parse("""{ "customer": { "id": null }, "quantity": 1 }""")!)
    ///     .Replacement("/customer/id", "$inputs.customerId")
    ///     .Replacement("$.quantity", ArazzoValue.FromLiteral(3), "jsonpath");
    /// </code>
    /// </example>
    public ArazzoStepBuilder Replacement(string target, ArazzoValue value, ArazzoSelectorType? targetSelectorType = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(target);
        ArgumentNullException.ThrowIfNull(value);
        _replacements.Add(new ArazzoPayloadReplacement
        {
            Target = target,
            Value = value,
            TargetSelectorType = targetSelectorType,
        });
        return this;
    }

    /// <summary>Adds a success criterion. Defaults to the "simple" condition type; runtime expressions inside <paramref name="condition"/> are evaluated during execution, not here.</summary>
    public ArazzoStepBuilder SuccessCriteria(string condition, string? context = null, ArazzoSelectorType? type = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(condition);
        _successCriteria.Add(new ArazzoCriterion { Condition = condition, Context = context, Type = type });
        return this;
    }

    /// <summary>Declares a named output whose value is the given runtime expression, e.g. <c>$message.payload#/id</c>.</summary>
    public ArazzoStepBuilder Output(string name, string expression)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(expression);
        _outputs[name] = ArazzoValue.FromExpression(expression);
        return this;
    }

    /// <summary>
    /// Declares a named output whose value is the data a Selector Object picks out (Arazzo 1.1), e.g. one
    /// item of a JSON array in the response. Outputs cannot be literal values, so there is no literal overload.
    /// </summary>
    /// <example>
    /// <code>
    /// step.Output("firstItemId", new ArazzoSelector
    /// {
    ///     Context = "$response.body",
    ///     Selector = "$.items[0].id",
    ///     Type = "jsonpath",
    /// });
    /// </code>
    /// </example>
    public ArazzoStepBuilder Output(string name, ArazzoSelector selector)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(selector);
        _outputs[name] = ArazzoValue.FromSelector(selector);
        return this;
    }

    /// <summary>Adds a parameter whose value is the given runtime expression.</summary>
    public ArazzoStepBuilder Parameter(string name, string valueExpression, string? location = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(valueExpression);
        _parameters.Add(ArazzoReferenceable<ArazzoParameter>.Of(new ArazzoParameter
        {
            Name = name,
            In = location,
            Value = ArazzoValue.FromExpression(valueExpression)
        }));
        return this;
    }

    /// <summary>
    /// Adds a parameter given as a Parameter Object, whose value can be a literal
    /// (<see cref="ArazzoValue.FromLiteral"/>), a runtime expression (<see cref="ArazzoValue.FromExpression"/>),
    /// or a Selector Object (<see cref="ArazzoValue.FromSelector"/>, Arazzo 1.1). Leave
    /// <see cref="ArazzoParameter.In"/> unset when the step targets a workflow, whose parameters map to that
    /// workflow's inputs.
    /// </summary>
    /// <param name="parameter">The parameter to add.</param>
    /// <example>
    /// <code>
    /// step.Parameter(new ArazzoParameter
    ///     {
    ///         Name = "limit",
    ///         In = ArazzoParameterLocation.Query,
    ///         Value = ArazzoValue.FromLiteral(10),
    ///     })
    ///     .Parameter(new ArazzoParameter
    ///     {
    ///         Name = "orderId",
    ///         In = ArazzoParameterLocation.Path,
    ///         Value = ArazzoValue.FromSelector(new ArazzoSelector
    ///         {
    ///             Context = "$steps.listOrders.outputs.orders",
    ///             Selector = "/0/id",
    ///             Type = "jsonpointer",
    ///         }),
    ///     });
    /// </code>
    /// </example>
    public ArazzoStepBuilder Parameter(ArazzoParameter parameter)
    {
        ArazzoParameterGuard.Validate(parameter);
        _parameters.Add(ArazzoReferenceable<ArazzoParameter>.Of(parameter));
        return this;
    }

    /// <summary>Adds a success action to run when this step succeeds.</summary>
    public ArazzoStepBuilder OnSuccess(ArazzoSuccessAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _onSuccess.Add(ArazzoReferenceable<ArazzoSuccessAction>.Of(action));
        return this;
    }

    /// <summary>Adds a failure action to run when this step fails.</summary>
    public ArazzoStepBuilder OnFailure(ArazzoFailureAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _onFailure.Add(ArazzoReferenceable<ArazzoFailureAction>.Of(action));
        return this;
    }

    /// <summary>Sets the maximum time, in milliseconds, to wait for this step before aborting and failing it.</summary>
    public ArazzoStepBuilder WithTimeout(int milliseconds)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(milliseconds, 0);
        TimeoutValue = milliseconds;
        return this;
    }

    private static string EscapePointerSegment(string segment) => segment.Replace("~", "~0").Replace("/", "~1");

    private ArazzoRequestBody? BuildRequestBody()
        => PayloadValue is null && PayloadContentType is null && _replacements.Count == 0
            ? null
            : new ArazzoRequestBody
            {
                ContentType = PayloadContentType,
                Payload = PayloadValue,
                Replacements = _replacements.Count > 0 ? [.. _replacements] : null,
            };

    internal ArazzoStep Build() => new()
    {
        StepId = _stepId,
        Description = Description,
        OperationId = OperationId,
        OperationPath = OperationPathValue,
        ChannelPath = ChannelPathValue,
        WorkflowId = WorkflowIdValue,
        Action = ActionValue,
        CorrelationId = CorrelationIdValue,
        RequestBody = BuildRequestBody(),
        SuccessCriteria = _successCriteria.Count > 0 ? _successCriteria : null,
        Outputs = _outputs.Count > 0 ? _outputs : null,
        DependsOn = _dependsOn.Count > 0 ? _dependsOn : null,
        Timeout = TimeoutValue,
        Parameters = _parameters.Count > 0 ? _parameters : null,
        OnSuccess = _onSuccess.Count > 0 ? _onSuccess : null,
        OnFailure = _onFailure.Count > 0 ? _onFailure : null,
    };
}
