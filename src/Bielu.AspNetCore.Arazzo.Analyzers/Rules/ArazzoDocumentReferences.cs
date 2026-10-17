using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Bielu.AspNetCore.Arazzo.Analyzers.Rules;

/// <summary>
/// Collects every <c>AddArazzo</c> call in the compilation, merges them per document, and reports references to
/// source descriptions (BARAZZO006), workflows and steps (BARAZZO007) the document never declares.
/// </summary>
/// <remarks>
/// The whole check is skipped when anything else configures <c>ArazzoOptions</c> (<c>Configure&lt;ArazzoOptions&gt;</c>,
/// <c>PostConfigure</c>, a helper taking an <c>Action&lt;ArazzoOptions&gt;</c>), or a document name isn't a constant,
/// because the declarations may then live where the analyzer can't see them.
/// </remarks>
internal sealed class ArazzoDocumentReferences
{
    public static readonly DiagnosticDescriptor UndeclaredSourceDescriptor = new(
        id: "BARAZZO006",
        title: "Undeclared source description",
        messageFormat: "'{0}' is not a source description of the Arazzo document '{1}'; declare it with AddAsyncApiSource, AddOpenApiSource or AddSourceDescription",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A Channel or OperationPath step builds a $sourceDescriptions.<name> reference, which only resolves when the document declares a source description of that name.",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    public static readonly DiagnosticDescriptor UnknownIdDescriptor = new(
        id: "BARAZZO007",
        title: "Unknown step or workflow",
        messageFormat: "'{0}' is not a {1} of the Arazzo document '{2}'",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "DependsOn and Workflow refer to steps and workflows by id. Neither the builders nor document validation check that the id exists, so a typo produces a document whose dependency can never be satisfied.",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    private readonly ConcurrentBag<ArazzoDocumentPart> _parts = [];
    private volatile bool _configuredElsewhere;

    public static void Register(AnalysisContext context) =>
        context.RegisterCompilationStartAction(start =>
        {
            var collector = new ArazzoDocumentReferences();
            start.RegisterOperationAction(collector.Collect, OperationKind.Invocation);
            start.RegisterCompilationEndAction(collector.Report);
        });

    private void Collect(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        if (ArazzoSymbols.IsAddArazzo(invocation))
        {
            _parts.Add(ArazzoDocumentPart.From(invocation));
        }
        else if (ConfiguresOptions(invocation.TargetMethod))
        {
            _configuredElsewhere = true;
        }
    }

    // Configure<ArazzoOptions>(...), or any method taking an Action<ArazzoOptions>.
    private static bool ConfiguresOptions(IMethodSymbol method) =>
        method.TypeArguments.Any(IsOptions)
        || method.Parameters.Any(p => p.Type is INamedTypeSymbol { DelegateInvokeMethod: { } invoke }
            && invoke.Parameters.Any(dp => IsOptions(dp.Type)));

    private static bool IsOptions(ITypeSymbol type) => type.ToDisplayString() == ArazzoSymbols.OptionsType;

    private void Report(CompilationAnalysisContext context)
    {
        var parts = _parts.ToList();
        if (_configuredElsewhere || parts.Any(p => p.DocumentName is null))
        {
            return;
        }

        foreach (var document in parts.GroupBy(p => p.DocumentName!))
        {
            ReportDocument(context, document.Key, document.ToList());
        }
    }

    private static void ReportDocument(CompilationAnalysisContext context, string documentName, List<ArazzoDocumentPart> parts)
    {
        if (parts.All(p => p.SourcesComplete))
        {
            var sources = new HashSet<string>(parts.SelectMany(p => p.Sources));
            foreach (var reference in parts.SelectMany(p => p.SourceReferences).Where(r => !sources.Contains(r.Id)))
            {
                context.ReportDiagnostic(Diagnostic.Create(UndeclaredSourceDescriptor, reference.Location, reference.Id, documentName));
            }
        }

        if (parts.All(p => p.WorkflowIdsComplete))
        {
            var workflowIds = new HashSet<string>(parts.SelectMany(p => p.WorkflowIds));
            foreach (var reference in parts.SelectMany(p => p.WorkflowReferences).Where(r => !workflowIds.Contains(r.Id)))
            {
                context.ReportDiagnostic(Diagnostic.Create(UnknownIdDescriptor, reference.Location, reference.Id, "workflow", documentName));
            }
        }

        // A step's DependsOn names steps of its own workflow.
        foreach (var workflow in parts.SelectMany(p => p.Workflows).Where(w => w.StepIdsComplete))
        {
            foreach (var reference in workflow.StepReferences.Where(r => !workflow.StepIds.Contains(r.Id)))
            {
                context.ReportDiagnostic(Diagnostic.Create(UnknownIdDescriptor, reference.Location, reference.Id, "step in this workflow", documentName));
            }
        }
    }
}
