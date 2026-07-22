using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Bielu.AspNetCore.AsyncApi.Analyzers.Rules;

internal static class RuleBASYNC001MissingAsyncApi
{
    public static readonly DiagnosticDescriptor Descriptor = new(
        id: "BASYNC001",
        title: "Missing [AsyncApi] attribute",
        messageFormat: "The type '{0}' contains AsyncAPI attributes but lacks the required [AsyncApi] attribute",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Classes or interfaces containing AsyncAPI channels, operations, or messages must be decorated with [AsyncApi].");

    public static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        var typeSymbol = (INamedTypeSymbol)context.Symbol;
        var attributes = typeSymbol.GetAttributes();

        bool hasAsyncApi = attributes.Any(ad => RuleUtils.IsAttribute(ad, RuleConstants.AsyncApiAttributeName));
        bool hasChannel = attributes.Any(ad => RuleUtils.IsAttribute(ad, RuleConstants.ChannelAttributeName));
        bool hasMessage = attributes.Any(ad => RuleUtils.IsAttribute(ad, RuleConstants.MessageAttributeName));

        if (!hasAsyncApi && (hasChannel || hasMessage))
        {
            context.ReportDiagnostic(Diagnostic.Create(Descriptor, typeSymbol.Locations[0], typeSymbol.Name));
        }
    }

    public static void AnalyzeMethod(SymbolAnalysisContext context)
    {
        var methodSymbol = (IMethodSymbol)context.Symbol;
        var typeSymbol = methodSymbol.ContainingType;

        var typeAttributes = typeSymbol.GetAttributes();
        var methodAttributes = methodSymbol.GetAttributes();

        bool typeHasAsyncApi = typeAttributes.Any(ad => RuleUtils.IsAttribute(ad, RuleConstants.AsyncApiAttributeName));
        bool methodHasChannel = methodAttributes.Any(ad => RuleUtils.IsAttribute(ad, RuleConstants.ChannelAttributeName));
        bool methodHasOperation = methodAttributes.Any(ad => RuleUtils.IsAttribute(ad, RuleConstants.PublishOperationAttributeName) ||
                                                           RuleUtils.IsAttribute(ad, RuleConstants.SubscribeOperationAttributeName) ||
                                                           RuleUtils.IsAttribute(ad, RuleConstants.OperationAttributeName));
        bool methodHasMessage = methodAttributes.Any(ad => RuleUtils.IsAttribute(ad, RuleConstants.MessageAttributeName));

        if (!typeHasAsyncApi && (methodHasChannel || methodHasOperation || methodHasMessage))
        {
            context.ReportDiagnostic(Diagnostic.Create(Descriptor, methodSymbol.Locations[0], typeSymbol.Name));
        }
    }
}
