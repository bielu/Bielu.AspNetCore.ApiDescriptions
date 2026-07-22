using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Bielu.AspNetCore.AsyncApi.Analyzers.Rules;

internal static class RuleBASYNC007MissingParameterlessConstructor
{
    public static readonly DiagnosticDescriptor Descriptor = new(
        id: "BASYNC007",
        title: "Missing parameterless constructor",
        messageFormat: "The provider type '{0}' must have a public parameterless constructor",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "IAsyncApiMessageExampleProvider implementations must have a public parameterless constructor to be instantiated at runtime.");

    public static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        var typeSymbol = (INamedTypeSymbol)context.Symbol;
        if (typeSymbol.IsAbstract || typeSymbol.TypeKind != TypeKind.Class) return;

        bool implementsInterface = false;
        foreach (var i in typeSymbol.AllInterfaces)
        {
            if (i.ToDisplayString() == RuleConstants.IMessageExampleProviderName)
            {
                implementsInterface = true;
                break;
            }
        }

        if (implementsInterface)
        {
            if (!typeSymbol.Constructors.Any(c => c.DeclaredAccessibility == Accessibility.Public && c.Parameters.Length == 0))
            {
                context.ReportDiagnostic(Diagnostic.Create(Descriptor, typeSymbol.Locations[0], typeSymbol.ToDisplayString()));
            }
        }
    }

    public static void AnalyzeMethod(SymbolAnalysisContext context)
    {
        var methodSymbol = (IMethodSymbol)context.Symbol;
        foreach (var ad in methodSymbol.GetAttributes())
        {
            if (RuleUtils.IsAttribute(ad, RuleConstants.MessageExampleAttributeName))
            {
                var providerTypeArg = ad.NamedArguments.FirstOrDefault(kvp => kvp.Key == "ProviderType");
                if (providerTypeArg.Key != null && providerTypeArg.Value.Value is INamedTypeSymbol providerType)
                {
                    if (!providerType.Constructors.Any(c => c.DeclaredAccessibility == Accessibility.Public && c.Parameters.Length == 0))
                    {
                        var location = ad.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? context.Symbol.Locations[0];
                        context.ReportDiagnostic(Diagnostic.Create(Descriptor, location, providerType.ToDisplayString()));
                    }
                }
            }
        }
    }
}
