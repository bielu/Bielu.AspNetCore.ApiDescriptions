using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Bielu.AspNetCore.AsyncApi.Analyzers.Rules;

internal static class RuleBASYNC009MissingDocumentation
{
    public static readonly DiagnosticDescriptor Descriptor = new(
        id: "BASYNC009",
        title: "Missing Summary or Description",
        messageFormat: "The component '{0}' is missing a 'Summary' or 'Description'. It is recommended to provide documentation for better API clarity.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Public AsyncAPI components should have a summary or description.");

    public static void AnalyzeMethod(SymbolAnalysisContext context)
    {
        var methodSymbol = (IMethodSymbol)context.Symbol;
        foreach (var ad in methodSymbol.GetAttributes())
        {
            if (RuleUtils.IsAttribute(ad, RuleConstants.OperationAttributeName) ||
                RuleUtils.IsAttribute(ad, RuleConstants.MessageAttributeName))
            {
                CheckDocumentation(context, ad, methodSymbol.Name);
            }
        }
    }

    public static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        var typeSymbol = (INamedTypeSymbol)context.Symbol;
        foreach (var ad in typeSymbol.GetAttributes())
        {
            if (RuleUtils.IsAttribute(ad, RuleConstants.AsyncApiAttributeName))
            {
                CheckDocumentation(context, ad, typeSymbol.Name);
            }
        }
    }

    private static void CheckDocumentation(SymbolAnalysisContext context, AttributeData ad, string componentName)
    {
        var summaryArg = ad.NamedArguments.FirstOrDefault(kvp => kvp.Key == "Summary");
        var descriptionArg = ad.NamedArguments.FirstOrDefault(kvp => kvp.Key == "Description");

        bool hasSummary = summaryArg.Key != null && !string.IsNullOrWhiteSpace(summaryArg.Value.Value as string);
        bool hasDescription = descriptionArg.Key != null && !string.IsNullOrWhiteSpace(descriptionArg.Value.Value as string);

        if (!hasSummary && !hasDescription)
        {
            var location = ad.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? context.Symbol.Locations[0];
            context.ReportDiagnostic(Diagnostic.Create(Descriptor, location, componentName));
        }
    }
}
