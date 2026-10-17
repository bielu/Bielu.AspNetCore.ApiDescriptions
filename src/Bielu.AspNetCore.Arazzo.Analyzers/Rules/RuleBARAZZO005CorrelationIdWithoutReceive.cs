using System.Linq;
using Bielu.Analyzers.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Bielu.AspNetCore.Arazzo.Analyzers.Rules;

/// <summary>
/// Reports <c>WithCorrelationId</c> on a step whose action isn't <c>receive</c>: a <c>send</c> channel step, or a
/// step that targets an operation or a workflow.
/// </summary>
/// <remarks>
/// Document validation only warns about this, and the correlation id is ignored. Steps whose target can't be read
/// statically (none, several, a non-constant action, or a lambda that branches) are skipped.
/// </remarks>
internal static class RuleBARAZZO005CorrelationIdWithoutReceive
{
    public static readonly DiagnosticDescriptor Descriptor = new(
        id: "BARAZZO005",
        title: "Correlation id on a step that doesn't receive",
        messageFormat: "Step '{0}' sets a correlation id, which only applies to a Channel step with the 'receive' action",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "An Arazzo step's correlationId links a received AsyncAPI message to the AsyncAPI document's correlation id. It has no meaning on a 'send' step or on a step that targets an OpenAPI operation or a workflow.");

    public static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        if (StepConfiguration.From((IInvocationOperation)context.Operation) is not { IsComplete: true, Targets.Count: 1 } step
            || step.Calls.FirstOrDefault(c => c.TargetMethod.Name == "WithCorrelationId") is not { } withCorrelationId)
        {
            return;
        }

        var target = step.Targets[0];
        if (target.TargetMethod.Name == "Channel"
            && ArazzoSymbols.ConstantString(target, "action") is null or ArazzoSymbols.ReceiveAction)
        {
            // A receiving channel step, or one whose action isn't known until runtime.
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Descriptor, InvocationLocations.NameLocation(withCorrelationId), step.StepId));
    }
}
