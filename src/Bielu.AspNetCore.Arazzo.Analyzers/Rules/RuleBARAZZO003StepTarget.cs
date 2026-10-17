using Bielu.Analyzers.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Bielu.AspNetCore.Arazzo.Analyzers.Rules;

/// <summary>
/// Reports a step that sets no target, or more than one, of <c>Operation</c>, <c>OperationPath</c>,
/// <c>Channel</c> and <c>Workflow</c>.
/// </summary>
/// <remarks>
/// A second target throws <see cref="System.InvalidOperationException"/> at startup, and a step with no target fails
/// document validation. Steps whose lambda branches or passes its builder elsewhere are skipped, because their calls
/// can't be counted statically.
/// </remarks>
internal static class RuleBARAZZO003StepTarget
{
    public static readonly DiagnosticDescriptor Descriptor = new(
        id: "BARAZZO003",
        title: "Step must set exactly one target",
        messageFormat: "Step '{0}' {1}; call exactly one of Operation, OperationPath, Channel or Workflow",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "An Arazzo step targets exactly one operation, channel or workflow. The step builder throws InvalidOperationException when a second target is set, and the document fails validation when none is.");

    public static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        if (StepConfiguration.From((IInvocationOperation)context.Operation) is not { IsComplete: true } step)
        {
            return;
        }

        if (step.Targets.Count == 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(Descriptor, InvocationLocations.NameLocation(step.StepCall),
                step.StepId, "sets no target"));
        }

        for (var i = 1; i < step.Targets.Count; i++)
        {
            context.ReportDiagnostic(Diagnostic.Create(Descriptor, InvocationLocations.NameLocation(step.Targets[i]),
                step.StepId, $"sets a second target with '{step.Targets[i].TargetMethod.Name}' after '{step.Targets[0].TargetMethod.Name}'"));
        }
    }
}
