using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.Operations;

namespace Bielu.AspNetCore.Arazzo.Analyzers;

/// <summary>
/// What a <c>Step(id, s =&gt; ...)</c> lambda configures, read from its inline body.
/// </summary>
internal sealed class StepConfiguration
{
    private static readonly string[] TargetMethods = ["Operation", "OperationPath", "Channel", "Workflow"];

    private StepConfiguration(string stepId, IInvocationOperation stepCall, List<IInvocationOperation> calls, bool isComplete)
    {
        StepId = stepId;
        StepCall = stepCall;
        Calls = calls;
        IsComplete = isComplete;
        Targets = calls.Where(c => TargetMethods.Contains(c.TargetMethod.Name)).ToList();
    }

    /// <summary>The step id, or <c>?</c> when it isn't a compile-time constant.</summary>
    public string StepId { get; }

    /// <summary>The <c>Step(...)</c> invocation itself.</summary>
    public IInvocationOperation StepCall { get; }

    /// <summary>Every <c>ArazzoStepBuilder</c> call in the lambda, in source order.</summary>
    public List<IInvocationOperation> Calls { get; }

    /// <summary>The target-setting calls (<c>Operation</c>, <c>OperationPath</c>, <c>Channel</c>, <c>Workflow</c>), in source order.</summary>
    public List<IInvocationOperation> Targets { get; }

    /// <summary>
    /// Whether <see cref="Calls"/> is exactly what runs: the lambda has no branches and doesn't hand its builder
    /// to other code. Rules that would report a missing call must check this first.
    /// </summary>
    public bool IsComplete { get; }

    /// <summary>Reads the step configured by <paramref name="stepCall"/>, or <see langword="null"/> when it isn't an inline lambda.</summary>
    public static StepConfiguration? From(IInvocationOperation stepCall)
    {
        if (stepCall.TargetMethod.Name != "Step"
            || !ArazzoSymbols.IsOn(stepCall, ArazzoSymbols.WorkflowBuilderType)
            || ArazzoSymbols.Lambda(stepCall, "configure") is not { } lambda)
        {
            return null;
        }

        var stepId = ArazzoSymbols.ConstantString(stepCall, "stepId") ?? ArazzoSymbols.MarkerTypeId(stepCall) ?? "?";
        var isComplete = !ArazzoSymbols.HasBranches(lambda)
            && ArazzoSymbols.UsesBuilderOnlyAsReceiver(lambda, ArazzoSymbols.StepBuilderType);
        return new StepConfiguration(stepId, stepCall, ArazzoSymbols.BuilderCalls(lambda, ArazzoSymbols.StepBuilderType), isComplete);
    }
}
