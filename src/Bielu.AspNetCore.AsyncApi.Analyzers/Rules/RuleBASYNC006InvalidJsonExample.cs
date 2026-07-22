using System.Linq;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Bielu.AspNetCore.AsyncApi.Analyzers.Rules;

internal static class RuleBASYNC006InvalidJsonExample
{
    public static readonly DiagnosticDescriptor Descriptor = new(
        id: "BASYNC006",
        title: "Invalid JSON in MessageExample",
        messageFormat: "The JSON literal provided in MessageExampleAttribute is not valid: {0}",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Ensure the 'Json' property of MessageExampleAttribute contains valid JSON.");

    public static void AnalyzeMethod(SymbolAnalysisContext context)
    {
        var methodSymbol = (IMethodSymbol)context.Symbol;
        foreach (var ad in methodSymbol.GetAttributes())
        {
            if (RuleUtils.IsAttribute(ad, RuleConstants.MessageExampleAttributeName))
            {
                var jsonArg = ad.NamedArguments.FirstOrDefault(kvp => kvp.Key == "Json");
                if (jsonArg.Key != null && jsonArg.Value.Value is string jsonContent)
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(jsonContent);
                    }
                    catch (JsonException ex)
                    {
                        var location = ad.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? context.Symbol.Locations[0];
                        context.ReportDiagnostic(Diagnostic.Create(Descriptor, location, ex.Message));
                    }
                }
            }
        }
    }
}
