using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization.Metadata;
using Bielu.Arazzo.Models;

namespace Bielu.AspNetCore.Arazzo.Services;

/// <summary>Fluent builder for a single <see cref="ArazzoWorkflow"/>, added via <see cref="ArazzoOptions.AddWorkflow"/>.</summary>
public sealed class ArazzoWorkflowBuilder
{
    private readonly string _workflowId;
    private readonly List<ArazzoStep> _steps = [];
    private readonly List<string> _dependsOn = [];
    private readonly List<ArazzoReferenceable<ArazzoParameter>> _parameters = [];
    private readonly List<ArazzoReferenceable<ArazzoSuccessAction>> _successActions = [];
    private readonly List<ArazzoReferenceable<ArazzoFailureAction>> _failureActions = [];
    private readonly Dictionary<string, ArazzoValue> _outputs = new(StringComparer.Ordinal);

    internal ArazzoWorkflowBuilder(string workflowId) => _workflowId = workflowId;

    private string? Summary { get; set; }

    private string? Description { get; set; }

    private JsonNode? Inputs { get; set; }

    /// <summary>Sets the workflow's summary.</summary>
    public ArazzoWorkflowBuilder WithSummary(string summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        Summary = summary;
        return this;
    }

    /// <summary>Sets the workflow's description.</summary>
    public ArazzoWorkflowBuilder WithDescription(string description)
    {
        ArgumentNullException.ThrowIfNull(description);
        Description = description;
        return this;
    }

    /// <summary>
    /// Sets the workflow's <c>inputs</c> schema from a source-generated <see cref="JsonTypeInfo"/>, avoiding
    /// reflection so this overload stays trim/AOT-safe.
    /// </summary>
    public ArazzoWorkflowBuilder WithInputs(JsonTypeInfo typeInfo)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);
        Inputs = JsonSchemaExporter.GetJsonSchemaAsNode(typeInfo);
        return this;
    }

    /// <summary>
    /// Sets the workflow's <c>inputs</c> schema by reflecting over <typeparamref name="TInputs"/>. Requires
    /// reflection-based JSON contract resolution; use the <see cref="JsonTypeInfo"/> overload with a
    /// source-generated <c>JsonSerializerContext</c> in trimmed/Native AOT apps.
    /// </summary>
    /// <param name="serializerOptions">
    /// Options that shape the schema, such as a naming policy. When they have no
    /// <see cref="JsonSerializerOptions.TypeInfoResolver" /> (for example
    /// <c>new JsonSerializerOptions(JsonSerializerDefaults.Web)</c>), a copy with the default reflection-based
    /// resolver is used, and the instance passed in is left unchanged. Defaults to
    /// <see cref="JsonSerializerOptions.Default" />.
    /// </param>
    [RequiresUnreferencedCode(
        "Generating a JSON schema from an arbitrary type requires reflection-based JSON contract resolution unless a source-generated JsonTypeInfo is supplied via the JsonTypeInfo overload.")]
    [RequiresDynamicCode(
        "Generating a JSON schema from an arbitrary type requires reflection-based JSON contract resolution unless a source-generated JsonTypeInfo is supplied via the JsonTypeInfo overload.")]
    public ArazzoWorkflowBuilder WithInputs<TInputs>(JsonSerializerOptions? serializerOptions = null)
    {
        var options = serializerOptions ?? JsonSerializerOptions.Default;
        if (options.TypeInfoResolver is null)
        {
            // JsonSchemaExporter needs a resolver. Copy rather than set it, so options the caller still uses
            // (and may later lock with a different resolver) are left alone.
            options = new JsonSerializerOptions(options) { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
        }

        Inputs = JsonSchemaExporter.GetJsonSchemaAsNode(options, typeof(TInputs));
        return this;
    }

    /// <summary>Adds workflowIds that must complete before this workflow can be processed.</summary>
    public ArazzoWorkflowBuilder DependsOn(params string[] workflowIds)
    {
        ArgumentNullException.ThrowIfNull(workflowIds);
        foreach (var workflowId in workflowIds)
        {
            ArazzoIdentifier.Validate(workflowId, nameof(workflowIds));
        }

        _dependsOn.AddRange(workflowIds);
        return this;
    }

    /// <summary>
    /// Adds the workflow identified by the marker type <typeparamref name="TWorkflow"/> (see
    /// <see cref="ArazzoId"/>) as one that must complete before this workflow can be processed. Chain the
    /// call to depend on several workflows.
    /// </summary>
    /// <typeparam name="TWorkflow">The marker type naming the workflow depended on.</typeparam>
    public ArazzoWorkflowBuilder DependsOn<TWorkflow>() => DependsOn(ArazzoId.FromType<TWorkflow>());

    /// <summary>
    /// Declares a named workflow output whose value is the given runtime expression, usually an output of one
    /// of this workflow's steps, e.g. <c>$steps.placeOrder.outputs.orderId</c>. Other workflows read it as
    /// <c>$workflows.{workflowId}.outputs.{name}</c>.
    /// </summary>
    /// <param name="name">The output name; one matching <c>^[a-zA-Z0-9\.\-_]+$</c>.</param>
    /// <param name="expression">The runtime expression that produces the value.</param>
    public ArazzoWorkflowBuilder Output(string name, string expression)
    {
        ArazzoIdentifier.ValidateKey(name, nameof(name));
        ArgumentException.ThrowIfNullOrEmpty(expression);
        _outputs[name] = ArazzoValue.FromExpression(expression);
        return this;
    }

    /// <summary>
    /// Declares a named workflow output whose value is the data a Selector Object picks out (Arazzo 1.1).
    /// Outputs cannot be literal values, so there is no literal overload.
    /// </summary>
    /// <param name="name">The output name.</param>
    /// <param name="selector">The selector that produces the value.</param>
    public ArazzoWorkflowBuilder Output(string name, ArazzoSelector selector)
    {
        ArazzoIdentifier.ValidateKey(name, nameof(name));
        ArgumentNullException.ThrowIfNull(selector);
        _outputs[name] = ArazzoValue.FromSelector(selector);
        return this;
    }

    /// <summary>
    /// Adds a parameter that applies to every step of this workflow whose target accepts it, with a runtime
    /// expression value. A step may override it with a parameter of the same name and location, but not remove it.
    /// </summary>
    /// <param name="name">The parameter name.</param>
    /// <param name="valueExpression">The runtime expression producing the value, e.g. <c>$inputs.tenant</c>.</param>
    /// <param name="location">Where the parameter goes (see <see cref="ArazzoParameterLocation"/>).</param>
    public ArazzoWorkflowBuilder Parameter(string name, string valueExpression, string? location = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(valueExpression);
        return Parameter(new ArazzoParameter
        {
            Name = name,
            In = location,
            Value = ArazzoValue.FromExpression(valueExpression),
        });
    }

    /// <summary>
    /// Adds a parameter that applies to every step of this workflow, given as a Parameter Object whose value
    /// can be a literal, a runtime expression, or a Selector Object (Arazzo 1.1).
    /// </summary>
    /// <param name="parameter">The parameter to add.</param>
    public ArazzoWorkflowBuilder Parameter(ArazzoParameter parameter)
    {
        ArazzoParameterGuard.Validate(parameter);
        _parameters.Add(ArazzoReferenceable<ArazzoParameter>.Of(parameter));
        return this;
    }

    /// <summary>
    /// Adds a success action (the workflow's <c>successActions</c>) that applies to every step of this
    /// workflow. A step's own <c>onSuccess</c> action of the same name overrides it.
    /// </summary>
    /// <param name="action">The success action to add.</param>
    public ArazzoWorkflowBuilder OnSuccess(ArazzoSuccessAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _successActions.Add(ArazzoReferenceable<ArazzoSuccessAction>.Of(action));
        return this;
    }

    /// <summary>
    /// Adds a failure action (the workflow's <c>failureActions</c>) that applies to every step of this
    /// workflow. A step's own <c>onFailure</c> action of the same name overrides it.
    /// </summary>
    /// <param name="action">The failure action to add.</param>
    /// <example>
    /// <code>
    /// workflow.OnFailure(new ArazzoFailureAction
    /// {
    ///     Name = "retryOnTimeout",
    ///     Type = ArazzoFailureActionType.Retry,
    ///     RetryAfter = 2,
    ///     RetryLimit = 3,
    ///     Criteria = [new ArazzoCriterion { Condition = "$statusCode == 504" }],
    /// });
    /// </code>
    /// </example>
    public ArazzoWorkflowBuilder OnFailure(ArazzoFailureAction action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _failureActions.Add(ArazzoReferenceable<ArazzoFailureAction>.Of(action));
        return this;
    }

    /// <summary>Adds a step to the workflow, in execution order.</summary>
    public ArazzoWorkflowBuilder Step(string stepId, Action<ArazzoStepBuilder> configure)
    {
        ArazzoIdentifier.Validate(stepId, nameof(stepId));
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new ArazzoStepBuilder(stepId);
        configure(builder);
        _steps.Add(builder.Build());
        return this;
    }

    /// <summary>
    /// Adds a step identified by the marker type <typeparamref name="TStep"/>, whose <c>stepId</c> follows
    /// the <see cref="ArazzoId"/> convention, in execution order. Lets later steps reference it as
    /// <c>DependsOn&lt;TStep&gt;()</c> instead of repeating a string id.
    /// </summary>
    /// <typeparam name="TStep">The marker type naming this step.</typeparam>
    public ArazzoWorkflowBuilder Step<TStep>(Action<ArazzoStepBuilder> configure)
        => Step(ArazzoId.FromType<TStep>(), configure);

    internal ArazzoWorkflow Build()
    {
        if (_steps.Count == 0)
        {
            throw new InvalidOperationException(
                $"Workflow '{_workflowId}' has no steps; add at least one via Step(...).");
        }

        return new ArazzoWorkflow
        {
            WorkflowId = _workflowId,
            Summary = Summary,
            Description = Description,
            Inputs = Inputs,
            DependsOn = _dependsOn.Count > 0 ? _dependsOn : null,
            Parameters = _parameters.Count > 0 ? _parameters : null,
            SuccessActions = _successActions.Count > 0 ? _successActions : null,
            FailureActions = _failureActions.Count > 0 ? _failureActions : null,
            Outputs = _outputs.Count > 0 ? _outputs : null,
            Steps = _steps,
        };
    }
}
