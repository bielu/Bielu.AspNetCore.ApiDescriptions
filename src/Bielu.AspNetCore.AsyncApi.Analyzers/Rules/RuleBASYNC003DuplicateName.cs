using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Bielu.AspNetCore.AsyncApi.Analyzers.Rules;

internal static class RuleBASYNC003DuplicateName
{
    public static readonly DiagnosticDescriptor Descriptor = new(
        id: "BASYNC003",
        title: "Duplicate Name attribute",
        messageFormat: "The name '{0}' is used multiple times for '{1}' on the same element",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Names must be unique across multiple attributes of the same type on a single element.");

    public static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        var typeSymbol = (INamedTypeSymbol)context.Symbol;
        var attributes = typeSymbol.GetAttributes();

        CheckDuplicateNames(context, attributes, RuleConstants.ChannelParameterAttributeName, "ChannelParameter");
    }

    public static void AnalyzeMethod(SymbolAnalysisContext context)
    {
        var methodSymbol = (IMethodSymbol)context.Symbol;
        var attributes = methodSymbol.GetAttributes();

        CheckDuplicateNames(context, attributes, RuleConstants.MessageAttributeName, "Message");
        CheckDuplicateNames(context, attributes, RuleConstants.ChannelParameterAttributeName, "ChannelParameter");
    }

    private static void CheckDuplicateNames(SymbolAnalysisContext context, ImmutableArray<AttributeData> attributes, string attributeFullName, string label)
    {
        var names = new HashSet<string>();
        foreach (var ad in attributes)
        {
            if (RuleUtils.IsAttribute(ad, attributeFullName))
            {
                string? name = null;
                // Try to get Name from constructor
                if (ad.AttributeConstructor != null)
                {
                    for (int i = 0; i < ad.AttributeConstructor.Parameters.Length; i++)
                    {
                        if (ad.AttributeConstructor.Parameters[i].Name.Equals("name", System.StringComparison.OrdinalIgnoreCase))
                        {
                            name = ad.ConstructorArguments[i].Value?.ToString();
                            break;
                        }
                    }
                }

                // Try to get Name from named arguments
                var nameArg = ad.NamedArguments.FirstOrDefault(kvp => kvp.Key.Equals("Name", System.StringComparison.OrdinalIgnoreCase));
                if (nameArg.Key != null)
                {
                    name = nameArg.Value.Value?.ToString();
                }

                if (!string.IsNullOrEmpty(name))
                {
                    if (!names.Add(name!))
                    {
                        var location = ad.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? context.Symbol.Locations[0];
                        context.ReportDiagnostic(Diagnostic.Create(Descriptor, location, name, label));
                    }
                }
            }
        }
    }
}
