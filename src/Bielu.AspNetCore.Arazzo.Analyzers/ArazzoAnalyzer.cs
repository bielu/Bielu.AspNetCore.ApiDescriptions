using System.Collections.Immutable;
using Bielu.AspNetCore.Arazzo.Analyzers.Rules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Bielu.AspNetCore.Arazzo.Analyzers;

/// <summary>The BARAZZO rules, which check Bielu.AspNetCore.Arazzo registration and workflow-builder code.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ArazzoAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
        RuleBARAZZO001AddOpenApiAfterAddArazzo.Descriptor,
        RuleBARAZZO002InvalidIdentifier.Descriptor,
        RuleBARAZZO003StepTarget.Descriptor,
        RuleBARAZZO004InvalidStepAction.Descriptor,
        RuleBARAZZO005CorrelationIdWithoutReceive.Descriptor,
        ArazzoDocumentReferences.UndeclaredSourceDescriptor,
        ArazzoDocumentReferences.UnknownIdDescriptor,
        RuleBARAZZO008UnregisteredSourceDocument.Descriptor);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterOperationBlockAction(RuleBARAZZO001AddOpenApiAfterAddArazzo.AnalyzeOperationBlock);
        context.RegisterOperationAction(oc =>
        {
            RuleBARAZZO002InvalidIdentifier.AnalyzeInvocation(oc);
            RuleBARAZZO003StepTarget.AnalyzeInvocation(oc);
            RuleBARAZZO004InvalidStepAction.AnalyzeInvocation(oc);
            RuleBARAZZO005CorrelationIdWithoutReceive.AnalyzeInvocation(oc);
        }, OperationKind.Invocation);

        ArazzoDocumentReferences.Register(context);
        RuleBARAZZO008UnregisteredSourceDocument.Register(context);
    }
}
