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
    private readonly Dictionary<string, JsonNode?> _extensions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, JsonNode?> _requestBodyExtensions = new(StringComparer.Ordinal);

    internal ArazzoStepBuilder(string stepId) => _stepId = stepId;

    private string? Description { get; set; }

    private string? OperationId { get; set; }

    private string? OperationPathValue { get; set; }

    private string? ChannelPathValue { get; set; }

    private string? WorkflowIdValue { get; set; }

    private ArazzoStepAction? ActionValue { get; set; }

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
    public ArazzoStepBuilder Channel(string sourceName, string channelName, ArazzoStepAction action)
    {
        ArazzoIdentifier.Validate(sourceName, nameof(sourceName));
        ArgumentException.ThrowIfNullOrEmpty(channelName);
        ArgumentException.ThrowIfNullOrEmpty(action.Value, nameof(action));
        EnsureNoTargetSet(nameof(Channel));
        ChannelPathValue = $"{{$sourceDescriptions.{sourceName}.url}}#/channels/{EscapePointerSegment(channelName)}";
        ActionValue = action;
        return this;
    }

    /// <summary>
    /// Targets an AsyncAPI channel, as <see cref="Channel(string, string, ArazzoStepAction)"/> does, and says
    /// whether the channel is created at runtime rather than declared with a <c>[Channel]</c> attribute.
    /// </summary>
    /// <param name="sourceName">The AsyncAPI source description.</param>
    /// <param name="channelName">The channel's key in the AsyncAPI document.</param>
    /// <param name="action">Whether the step sends to or receives from the channel.</param>
    /// <param name="dynamic">
    /// <see langword="true"/> when a runtime mapping, such as Wolverine's message routing or a document transformer,
    /// adds the channel to the document. Analyzer BARAZZO009 can only see <c>[Channel]</c> attributes, so it skips a
    /// dynamic channel instead of reporting it as unknown. The generated step is the same either way, and startup
    /// validation still checks the channel against the live document.
    /// </param>
    /// <example>
    /// <code>
    /// options.AddWorkflow("checkout", wf => wf
    ///     .Step("publish", s => s.Channel("events", "orders", ArazzoStepAction.Send, dynamic: true)));
    /// </code>
    /// </example>
    public ArazzoStepBuilder Channel(string sourceName, string channelName, ArazzoStepAction action, bool dynamic) =>
        Channel(sourceName, channelName, action);

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

    /// <summary>
    /// Targets a workflow of another Arazzo document, the source description named <paramref name="sourceName"/>
    /// (of type <c>arazzo</c>), as <c>$sourceDescriptions.{sourceName}.{workflowId}</c>. Mutually exclusive with
    /// <see cref="Operation"/>, <see cref="OperationPath"/>, and <see cref="Channel"/>.
    /// </summary>
    /// <param name="sourceName">The source description of the other Arazzo document.</param>
    /// <param name="workflowId">The workflow's id in that document.</param>
    /// <example>
    /// <code>
    /// options.AddSourceDescription("payments", "https://example.com/payments.arazzo.json", ArazzoSourceDescriptionType.Arazzo);
    /// options.AddWorkflow("checkout", wf => wf.Step("pay", s => s.Workflow("payments", "chargeCard")));
    /// </code>
    /// </example>
    public ArazzoStepBuilder Workflow(string sourceName, string workflowId)
    {
        ArazzoIdentifier.Validate(sourceName, nameof(sourceName));
        ArazzoIdentifier.Validate(workflowId, nameof(workflowId));
        EnsureNoTargetSet(nameof(Workflow));
        WorkflowIdValue = $"$sourceDescriptions.{sourceName}.{workflowId}";
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
        return Replacement(new ArazzoPayloadReplacement { Target = target, Value = value, TargetSelectorType = targetSelectorType });
    }

    /// <summary>
    /// Adds a Payload Replacement Object as given, for one that carries Specification Extensions (<c>x-*</c>).
    /// Like the other overload, it is kept whether the payload is set before or after it.
    /// </summary>
    /// <param name="replacement">The replacement to add.</param>
    public ArazzoStepBuilder Replacement(ArazzoPayloadReplacement replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        ArgumentException.ThrowIfNullOrEmpty(replacement.Target, nameof(replacement));
        if (replacement.Value is null)
        {
            throw new ArgumentException("The replacement must have a value.", nameof(replacement));
        }

        _replacements.Add(replacement);
        return this;
    }

    /// <summary>
    /// Adds a Specification Extension (<c>x-*</c>) to the step's request body. Setting the same name again replaces
    /// its value. A request body with only extensions is still written.
    /// </summary>
    /// <param name="name">The extension name, which must start with <c>x-</c>.</param>
    /// <param name="value">The extension value; any JSON, including <c>null</c>.</param>
    public ArazzoStepBuilder WithRequestBodyExtension(string name, JsonNode? value)
    {
        ArazzoExtensions.Set(_requestBodyExtensions, name, value);
        return this;
    }

    /// <summary>Adds a success criterion. Defaults to the "simple" condition type; runtime expressions inside <paramref name="condition"/> are evaluated during execution, not here.</summary>
    public ArazzoStepBuilder SuccessCriteria(string condition, string? context = null, ArazzoSelectorType? type = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(condition);
        _successCriteria.Add(new ArazzoCriterion { Condition = condition, Context = context, Type = type });
        return this;
    }

    /// <summary>
    /// Adds a success criterion given as a Criterion Object, for one that carries Specification Extensions
    /// (<c>x-*</c>) as well as its condition, context and type.
    /// </summary>
    /// <param name="criterion">The criterion to add.</param>
    public ArazzoStepBuilder SuccessCriteria(ArazzoCriterion criterion)
    {
        ArgumentNullException.ThrowIfNull(criterion);
        ArgumentException.ThrowIfNullOrEmpty(criterion.Condition, nameof(criterion));
        _successCriteria.Add(criterion);
        return this;
    }

    /// <summary>Declares a named output whose value is the given runtime expression, e.g. <c>$message.payload#/id</c>.</summary>
    public ArazzoStepBuilder Output(string name, string expression)
    {
        ArazzoIdentifier.ValidateKey(name, nameof(name));
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
        ArazzoIdentifier.ValidateKey(name, nameof(name));
        ArgumentNullException.ThrowIfNull(selector);
        _outputs[name] = ArazzoValue.FromSelector(selector);
        return this;
    }

    /// <summary>Adds a parameter whose value is the given runtime expression.</summary>
    public ArazzoStepBuilder Parameter(string name, string valueExpression, ArazzoParameterLocation? location = null)
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

    /// <summary>
    /// Adds a parameter that refers to one in <c>components.parameters</c>, added with
    /// <see cref="ArazzoOptions.AddComponentParameter"/>.
    /// </summary>
    /// <param name="componentName">The component's name.</param>
    /// <param name="value">A value that overrides the referenced parameter's own value, if any.</param>
    public ArazzoStepBuilder ParameterReference(string componentName, string? value = null)
    {
        _parameters.Add(ArazzoComponentReference.Parameter(componentName, value));
        return this;
    }

    /// <summary>
    /// Adds a success action that refers to one in <c>components.successActions</c>, added with
    /// <see cref="ArazzoOptions.AddComponentSuccessAction"/>.
    /// </summary>
    /// <param name="componentName">The component's name.</param>
    public ArazzoStepBuilder OnSuccessReference(string componentName)
    {
        _onSuccess.Add(ArazzoComponentReference.SuccessAction(componentName));
        return this;
    }

    /// <summary>
    /// Adds a failure action that refers to one in <c>components.failureActions</c>, added with
    /// <see cref="ArazzoOptions.AddComponentFailureAction"/>.
    /// </summary>
    /// <param name="componentName">The component's name.</param>
    public ArazzoStepBuilder OnFailureReference(string componentName)
    {
        _onFailure.Add(ArazzoComponentReference.FailureAction(componentName));
        return this;
    }

    /// <summary>
    /// Adds a Specification Extension (<c>x-*</c>) to the step. Setting the same name again replaces its value.
    /// </summary>
    /// <param name="name">The extension name, which must start with <c>x-</c>.</param>
    /// <param name="value">The extension value; any JSON, including <c>null</c>.</param>
    public ArazzoStepBuilder WithExtension(string name, JsonNode? value)
    {
        ArazzoExtensions.Set(_extensions, name, value);
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
        => PayloadValue is null && PayloadContentType is null && _replacements.Count == 0 && _requestBodyExtensions.Count == 0
            ? null
            : new ArazzoRequestBody
            {
                ContentType = PayloadContentType,
                Payload = PayloadValue,
                Replacements = _replacements.Count > 0 ? [.. _replacements] : null,
                Extensions = _requestBodyExtensions.Count > 0 ? _requestBodyExtensions : null,
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
        Extensions = _extensions.Count > 0 ? _extensions : null,
    };
}
