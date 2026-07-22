using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Bielu.AspNetCore.AsyncApi.Analyzers.Rules;

internal static class RuleBASYNC008DiscouragedCharacters
{
    public static readonly DiagnosticDescriptor Descriptor = new(
        id: "BASYNC008",
        title: "Discouraged characters in ID or Name",
        messageFormat: "The value '{0}' contains discouraged characters (e.g. spaces). It is recommended to use only alphanumeric characters, underscores, hyphens, and dots for IDs.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "AsyncAPI IDs and names should follow common programming naming conventions and avoid spaces.");

    public static void AnalyzeMethod(SymbolAnalysisContext context)
    {
        var methodSymbol = (IMethodSymbol)context.Symbol;
        foreach (var ad in methodSymbol.GetAttributes())
        {
            CheckAttribute(context, ad);
        }
    }

    public static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        var typeSymbol = (INamedTypeSymbol)context.Symbol;
        foreach (var ad in typeSymbol.GetAttributes())
        {
            CheckAttribute(context, ad);
        }
    }

    private static void CheckAttribute(SymbolAnalysisContext context, AttributeData ad)
    {
        if (RuleUtils.IsAttribute(ad, RuleConstants.MessageAttributeName))
        {
            CheckProperty(context, ad, "MessageId");
            CheckProperty(context, ad, "Name");
        }
        else if (RuleUtils.IsAttribute(ad, RuleConstants.OperationAttributeName))
        {
            CheckProperty(context, ad, "OperationId");
        }
        else if (RuleUtils.IsAttribute(ad, RuleConstants.ChannelAttributeName))
        {
            // Channel name is usually a path/topic, so it might have slashes. 
            // But we can check for spaces.
            if (ad.ConstructorArguments.Length > 0 && ad.ConstructorArguments[0].Value is string channelName)
            {
                if (channelName.Contains(" "))
                {
                    Report(context, ad, channelName);
                }
            }
        }
    }

    private static void CheckProperty(SymbolAnalysisContext context, AttributeData ad, string propertyName)
    {
        var arg = ad.NamedArguments.FirstOrDefault(kvp => kvp.Key == propertyName);
        if (arg.Key != null && arg.Value.Value is string value)
        {
            if (value.Any(c => char.IsWhiteSpace(c)))
            {
                Report(context, ad, value);
            }
        }
    }

    private static void Report(SymbolAnalysisContext context, AttributeData ad, string value)
    {
        var location = ad.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? context.Symbol.Locations[0];
        context.ReportDiagnostic(Diagnostic.Create(Descriptor, location, value));
    }
}
