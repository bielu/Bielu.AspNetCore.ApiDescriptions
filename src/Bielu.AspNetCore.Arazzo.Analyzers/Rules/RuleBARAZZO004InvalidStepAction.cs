using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Bielu.AspNetCore.Arazzo.Analyzers.Rules;

/// <summary>
/// Reports a <c>Channel(source, channel, action)</c> call whose literal action is neither <c>send</c> nor <c>receive</c>.
/// </summary>
/// <remarks>
/// The step builder accepts any non-empty string; document validation then rejects it at runtime. The comparison
/// is ordinal, as in the validator, so <c>"Send"</c> is reported too.
/// </remarks>
internal static class RuleBARAZZO004InvalidStepAction
{
    public static readonly DiagnosticDescriptor Descriptor = new(
        id: "BARAZZO004",
        title: "Invalid step action",
        messageFormat: "'{0}' is not a step action; use ArazzoStepAction.Send (\"send\") or ArazzoStepAction.Receive (\"receive\")",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "An Arazzo channel step's action is either 'send' or 'receive', compared case-sensitively. Any other value fails document validation.");

    public static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        if (invocation.TargetMethod.Name != "Channel" || !ArazzoSymbols.IsOn(invocation, ArazzoSymbols.StepBuilderType))
        {
            return;
        }

        if (ArazzoSymbols.Argument(invocation, "action")?.Value is { ConstantValue: { HasValue: true, Value: string action } } value
            && action is not (ArazzoSymbols.SendAction or ArazzoSymbols.ReceiveAction))
        {
            context.ReportDiagnostic(Diagnostic.Create(Descriptor, value.Syntax.GetLocation(), action));
        }
    }
}
