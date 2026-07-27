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

    internal ArazzoStepBuilder(string stepId) => _stepId = stepId;

    private string? Description { get; set; }

    private string? OperationId { get; set; }

    private string? OperationPathValue { get; set; }

    private string? ChannelPathValue { get; set; }

    private string? WorkflowIdValue { get; set; }

    private string? ActionValue { get; set; }

    private string? CorrelationIdValue { get; set; }

    private ArazzoRequestBody? RequestBody { get; set; }

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
        ArgumentException.ThrowIfNullOrEmpty(sourceName);
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentException.ThrowIfNullOrEmpty(httpMethod);
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
        ArgumentException.ThrowIfNullOrEmpty(sourceName);
        ArgumentException.ThrowIfNullOrEmpty(channelName);
        ArgumentException.ThrowIfNullOrEmpty(action);
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
        ArgumentException.ThrowIfNullOrEmpty(workflowId);
        WorkflowIdValue = workflowId;
        return this;
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
        _dependsOn.AddRange(stepIds);
        return this;
    }

    /// <summary>
    /// Adds the step identified by the marker type <typeparamref name="TStep"/> (see <see cref="ArazzoId"/>)
    /// as one that must complete before this step executes. Chain the call to depend on several steps.
    /// </summary>
    /// <typeparam name="TStep">The marker type naming the step depended on.</typeparam>
    public ArazzoStepBuilder DependsOn<TStep>() => DependsOn(ArazzoId.FromType<TStep>());

    /// <summary>Sets the step's request body payload from a literal JSON value.</summary>
    public ArazzoStepBuilder Payload(JsonNode payload, string? contentType = null)
    {
        ArgumentNullException.ThrowIfNull(payload);
        RequestBody = new ArazzoRequestBody { Payload = ArazzoValue.FromLiteral(payload), ContentType = contentType };
        return this;
    }

    /// <summary>Sets the step's request body payload to a runtime expression, e.g. <c>$inputs.body</c>.</summary>
    public ArazzoStepBuilder PayloadExpression(string expression, string? contentType = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(expression);
        RequestBody =
            new ArazzoRequestBody { Payload = ArazzoValue.FromExpression(expression), ContentType = contentType };
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

    /// <summary>Adds a parameter whose value is the given runtime expression.</summary>
    public ArazzoStepBuilder Parameter(string name, string valueExpression, string? location = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(valueExpression);
        _parameters.Add(ArazzoReferenceable<ArazzoParameter>.Of(new ArazzoParameter
        {
            Name = name, In = location, Value = ArazzoValue.FromExpression(valueExpression)
        }));
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
        TimeoutValue = milliseconds;
        return this;
    }

    private static string EscapePointerSegment(string segment) => segment.Replace("~", "~0").Replace("/", "~1");

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
        RequestBody = RequestBody,
        SuccessCriteria = _successCriteria.Count > 0 ? _successCriteria : null,
        Outputs = _outputs.Count > 0 ? _outputs : null,
        DependsOn = _dependsOn.Count > 0 ? _dependsOn : null,
        Timeout = TimeoutValue,
        Parameters = _parameters.Count > 0 ? _parameters : null,
        OnSuccess = _onSuccess.Count > 0 ? _onSuccess : null,
        OnFailure = _onFailure.Count > 0 ? _onFailure : null,
    };
}
