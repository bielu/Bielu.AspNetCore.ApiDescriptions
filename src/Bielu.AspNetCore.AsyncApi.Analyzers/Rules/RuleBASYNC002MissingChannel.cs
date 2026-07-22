using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Bielu.AspNetCore.AsyncApi.Analyzers.Rules;

internal static class RuleBASYNC002MissingChannel
{
    public static readonly DiagnosticDescriptor Descriptor = new(
        id: "BASYNC002",
        title: "Missing [Channel] attribute",
        messageFormat: "The method '{0}' has an operation attribute but lacks a [Channel] attribute on the method or its containing type",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Operation attributes (Publish/Subscribe) require a [Channel] attribute to define the target channel.");

    public static void AnalyzeMethod(SymbolAnalysisContext context)
    {
        var methodSymbol = (IMethodSymbol)context.Symbol;
        var typeSymbol = methodSymbol.ContainingType;

        var typeAttributes = typeSymbol.GetAttributes();
        var methodAttributes = methodSymbol.GetAttributes();

        bool typeHasChannel = typeAttributes.Any(ad => RuleUtils.IsAttribute(ad, RuleConstants.ChannelAttributeName));
        bool methodHasChannel = methodAttributes.Any(ad => RuleUtils.IsAttribute(ad, RuleConstants.ChannelAttributeName));
        bool methodHasOperation = methodAttributes.Any(ad => RuleUtils.IsAttribute(ad, RuleConstants.PublishOperationAttributeName) ||
                                                           RuleUtils.IsAttribute(ad, RuleConstants.SubscribeOperationAttributeName) ||
                                                           RuleUtils.IsAttribute(ad, RuleConstants.OperationAttributeName));

        if (methodHasOperation && !methodHasChannel && !typeHasChannel)
        {
            context.ReportDiagnostic(Diagnostic.Create(Descriptor, methodSymbol.Locations[0], methodSymbol.Name));
        }
    }
}
