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

    internal ArazzoWorkflowBuilder(string workflowId) => _workflowId = workflowId;

    private string? Summary { get; set; }

    private string? Description { get; set; }

    private JsonNode? Inputs { get; set; }

    /// <summary>Sets the workflow's summary.</summary>
    public ArazzoWorkflowBuilder WithSummary(string summary)
    {
        Summary = summary;
        return this;
    }

    /// <summary>Sets the workflow's description.</summary>
    public ArazzoWorkflowBuilder WithDescription(string description)
    {
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
    [RequiresUnreferencedCode(
        "Generating a JSON schema from an arbitrary type requires reflection-based JSON contract resolution unless a source-generated JsonTypeInfo is supplied via the JsonTypeInfo overload.")]
    [RequiresDynamicCode(
        "Generating a JSON schema from an arbitrary type requires reflection-based JSON contract resolution unless a source-generated JsonTypeInfo is supplied via the JsonTypeInfo overload.")]
    public ArazzoWorkflowBuilder WithInputs<TInputs>(JsonSerializerOptions? serializerOptions = null)
    {
        Inputs = JsonSchemaExporter.GetJsonSchemaAsNode(serializerOptions ?? JsonSerializerOptions.Default,
            typeof(TInputs));
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
            Steps = _steps,
        };
    }
}
