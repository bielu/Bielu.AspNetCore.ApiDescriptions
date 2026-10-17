using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Bielu.AspNetCore.Arazzo.Analyzers.Rules;

/// <summary>
/// Reports a literal workflow, step or source-description id that doesn't match the Arazzo
/// <c>identifier-strict</c> grammar (<c>^[A-Za-z0-9_-]+$</c>), and a marker type whose derived id doesn't.
/// </summary>
/// <remarks>
/// The builders throw <see cref="System.ArgumentException"/> for these at startup, so this moves the failure to
/// compile time. Only compile-time constants are checked.
/// </remarks>
internal static class RuleBARAZZO002InvalidIdentifier
{
    public static readonly DiagnosticDescriptor Descriptor = new(
        id: "BARAZZO002",
        title: "Invalid Arazzo identifier",
        messageFormat: "'{0}' is not a valid Arazzo identifier; use only letters, digits, '_' and '-'",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Workflow ids, step ids and source-description names must match the Arazzo identifier-strict grammar '^[A-Za-z0-9_-]+$', because runtime expressions such as $steps.<id> and $sourceDescriptions.<name> are parsed with it. The builders throw ArgumentException at startup for any other value. A marker type passed to a generic overload is camel-cased into an id, which fails for generic marker types.");

    // Identifier-taking parameters, per builder type. AddSourceDescription calls its parameter 'name', which on
    // ArazzoStepBuilder (Output, Parameter) is a free-form name, so the parameter names are scoped to their type.
    private static readonly Dictionary<string, string[]> IdentifierParameters = new()
    {
        [ArazzoSymbols.OptionsType] = ["workflowId", "name", "sourceName"],
        [ArazzoSymbols.WorkflowBuilderType] = ["stepId", "workflowIds"],
        [ArazzoSymbols.StepBuilderType] = ["sourceName", "workflowId", "stepIds"],
        [ArazzoSymbols.ArazzoIdType] = [],
    };

    public static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var containingType = invocation.TargetMethod.ContainingType?.ToDisplayString();
        if (containingType is null || !IdentifierParameters.TryGetValue(containingType, out var parameterNames))
        {
            return;
        }

        // A method has at most one identifier parameter, so the marker id is reported once.
        var parameterName = invocation.TargetMethod.Parameters.Select(p => p.Name).FirstOrDefault(parameterNames.Contains) ?? "";
        foreach (var reference in ArazzoSymbols.ConstantIds(invocation, parameterName))
        {
            if (!IsValid(reference.Id))
            {
                context.ReportDiagnostic(Diagnostic.Create(Descriptor, reference.Location, reference.Id));
            }
        }
    }

    private static bool IsValid(string id) =>
        id.Length > 0 && id.All(c => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '-');
}
