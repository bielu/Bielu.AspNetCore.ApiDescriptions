using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Bielu.AspNetCore.Arazzo.Analyzers;

/// <summary>An id used somewhere in a document, and where.</summary>
internal sealed class IdReference(string id, Location location)
{
    public string Id { get; } = id;

    public Location Location { get; } = location;
}

/// <summary>
/// What one <c>AddArazzo(name, o =&gt; ...)</c> call declares and references. Several calls with the same document
/// name configure the same options, so the parts are merged per document before ids are checked.
/// </summary>
/// <remarks>
/// Each "complete" flag says whether the declared set is the whole set: it's false when an id isn't a constant or
/// a builder is handed to code this analyzer can't see. A reference is only reported against a complete set.
/// Branches don't clear the flags; they can only make a declaration not run, never add one, so the declared set
/// stays a superset of the real one and checking against it can't produce a false positive.
/// </remarks>
internal sealed class ArazzoDocumentPart
{
    private ArazzoDocumentPart(string? documentName) => DocumentName = documentName;

    /// <summary>The lowercased document name, or <see langword="null"/> when it isn't a compile-time constant.</summary>
    public string? DocumentName { get; }

    public HashSet<string> Sources { get; } = [];

    public bool SourcesComplete { get; private set; } = true;

    public HashSet<string> WorkflowIds { get; } = [];

    public bool WorkflowIdsComplete { get; private set; } = true;

    public List<WorkflowPart> Workflows { get; } = [];

    /// <summary>Source-description names used by <c>Channel</c>/<c>OperationPath</c> steps.</summary>
    public List<IdReference> SourceReferences { get; } = [];

    /// <summary>Workflow ids used by a workflow's <c>DependsOn</c> or a step's <c>Workflow</c>.</summary>
    public List<IdReference> WorkflowReferences { get; } = [];

    public static ArazzoDocumentPart From(IInvocationOperation addArazzo)
    {
        var documentName = ArazzoSymbols.Argument(addArazzo, "documentName") is null
            ? ArazzoSymbols.DefaultDocumentName
            : ArazzoSymbols.ConstantString(addArazzo, "documentName")?.ToLowerInvariant();
        var part = new ArazzoDocumentPart(documentName);

        if (ArazzoSymbols.Argument(addArazzo, "configureOptions") is null)
        {
            return part;
        }

        if (ArazzoSymbols.Lambda(addArazzo, "configureOptions") is not { } lambda
            || !ArazzoSymbols.UsesBuilderOnlyAsReceiver(lambda, ArazzoSymbols.OptionsType))
        {
            part.SourcesComplete = false;
            part.WorkflowIdsComplete = false;
            return part;
        }

        foreach (var call in ArazzoSymbols.BuilderCalls(lambda, ArazzoSymbols.OptionsType))
        {
            switch (call.TargetMethod.Name)
            {
                case "AddSourceDescription" or "AddAsyncApiSource" or "AddOpenApiSource":
                    var source = ArazzoSymbols.ConstantString(call, call.TargetMethod.Name == "AddSourceDescription" ? "name" : "sourceName");
                    if (source is null)
                    {
                        part.SourcesComplete = false;
                    }
                    else
                    {
                        part.Sources.Add(source);
                    }

                    break;
                case "AddWorkflow":
                    var workflowId = ArazzoSymbols.ConstantString(call, "workflowId") ?? ArazzoSymbols.MarkerTypeId(call);
                    if (workflowId is null)
                    {
                        part.WorkflowIdsComplete = false;
                    }
                    else
                    {
                        part.WorkflowIds.Add(workflowId);
                    }

                    part.Workflows.Add(WorkflowPart.From(part, ArazzoSymbols.Lambda(call, "configure")));
                    break;
            }
        }

        return part;
    }
}

/// <summary>What one <c>AddWorkflow(id, w =&gt; ...)</c> call declares and references.</summary>
internal sealed class WorkflowPart
{
    public HashSet<string> StepIds { get; } = [];

    public bool StepIdsComplete { get; private set; } = true;

    /// <summary>Step ids used by a step's <c>DependsOn</c>.</summary>
    public List<IdReference> StepReferences { get; } = [];

    public static WorkflowPart From(ArazzoDocumentPart document, IAnonymousFunctionOperation? lambda)
    {
        var workflow = new WorkflowPart();
        if (lambda is null || !ArazzoSymbols.UsesBuilderOnlyAsReceiver(lambda, ArazzoSymbols.WorkflowBuilderType))
        {
            workflow.StepIdsComplete = false;
            return workflow;
        }

        foreach (var call in ArazzoSymbols.BuilderCalls(lambda, ArazzoSymbols.WorkflowBuilderType))
        {
            switch (call.TargetMethod.Name)
            {
                case "DependsOn":
                    document.WorkflowReferences.AddRange(ArazzoSymbols.ConstantIds(call, "workflowIds"));
                    break;
                case "Step":
                    if ((ArazzoSymbols.ConstantString(call, "stepId") ?? ArazzoSymbols.MarkerTypeId(call)) is { } stepId)
                    {
                        workflow.StepIds.Add(stepId);
                    }
                    else
                    {
                        workflow.StepIdsComplete = false;
                    }

                    if (ArazzoSymbols.Lambda(call, "configure") is { } stepLambda)
                    {
                        workflow.ReadStep(document, stepLambda);
                    }

                    break;
            }
        }

        return workflow;
    }

    private void ReadStep(ArazzoDocumentPart document, IAnonymousFunctionOperation stepLambda)
    {
        foreach (var call in ArazzoSymbols.BuilderCalls(stepLambda, ArazzoSymbols.StepBuilderType))
        {
            switch (call.TargetMethod.Name)
            {
                case "Channel" or "OperationPath":
                    document.SourceReferences.AddRange(ArazzoSymbols.ConstantIds(call, "sourceName"));
                    break;
                case "Workflow":
                    document.WorkflowReferences.AddRange(ArazzoSymbols.ConstantIds(call, "workflowId"));
                    break;
                case "DependsOn":
                    StepReferences.AddRange(ArazzoSymbols.ConstantIds(call, "stepIds"));
                    break;
            }
        }
    }
}
