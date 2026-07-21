using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Bielu.AspNetCore.AsyncApi.Analyzers.Rules;

internal static class RuleBASYNC005InvalidPayloadType
{
    public static readonly DiagnosticDescriptor Descriptor = new(
        id: "BASYNC005",
        title: "Invalid payload type",
        messageFormat: "The type '{0}' used for payload or parameter is not suitable for AsyncAPI schema generation",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Avoid using types like void, IntPtr, or other non-serializable types in AsyncAPI definitions.");

    public static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        var typeSymbol = (INamedTypeSymbol)context.Symbol;
        CheckInvalidTypes(context, typeSymbol.GetAttributes());
    }

    public static void AnalyzeMethod(SymbolAnalysisContext context)
    {
        var methodSymbol = (IMethodSymbol)context.Symbol;
        CheckInvalidTypes(context, methodSymbol.GetAttributes());
    }

    private static void CheckInvalidTypes(SymbolAnalysisContext context, ImmutableArray<AttributeData> attributes)
    {
        foreach (var ad in attributes)
        {
            if (RuleUtils.IsAttribute(ad, RuleConstants.MessageAttributeName))
            {
                if (ad.ConstructorArguments.Length > 0)
                {
                    var typeArg = ad.ConstructorArguments[0];
                    if (typeArg.Value is ITypeSymbol typeSymbol)
                    {
                        CheckType(context, typeSymbol, ad);
                    }
                }
                var headersTypeArg = ad.NamedArguments.FirstOrDefault(kvp => kvp.Key == "HeadersType");
                if (headersTypeArg.Key != null && headersTypeArg.Value.Value is ITypeSymbol headersTypeSymbol)
                {
                    CheckType(context, headersTypeSymbol, ad);
                }
            }
            else if (RuleUtils.IsAttribute(ad, RuleConstants.ChannelParameterAttributeName))
            {
                if (ad.ConstructorArguments.Length > 1)
                {
                    var typeArg = ad.ConstructorArguments[1];
                    if (typeArg.Value is ITypeSymbol typeSymbol)
                    {
                        CheckType(context, typeSymbol, ad);
                    }
                }
            }
        }
    }

    private static void CheckType(SymbolAnalysisContext context, ITypeSymbol type, AttributeData ad)
    {
        if (type.SpecialType == SpecialType.System_Void ||
            type.TypeKind == TypeKind.Pointer ||
            type.TypeKind == TypeKind.FunctionPointer ||
            type.ToDisplayString() == "System.IntPtr" ||
            type.ToDisplayString() == "System.UIntPtr")
        {
            var location = ad.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? context.Symbol.Locations[0];
            context.ReportDiagnostic(Diagnostic.Create(Descriptor, location, type.ToDisplayString()));
        }
    }
}
