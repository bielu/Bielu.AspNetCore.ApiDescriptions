using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Bielu.AspNetCore.AsyncApi.Analyzers.Rules;

internal sealed class RuleBASYNC004UnusedDocumentName
{
    public static readonly DiagnosticDescriptor Descriptor = new(
        id: "BASYNC004",
        title: "Unused document name",
        messageFormat: "The document name '{0}' specified in [AsyncApi] does not match any registered document in the application",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Ensure the document name matches one used in 'AddAsyncApi' during service registration.",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    private readonly ConcurrentBag<(string Name, Location Location)> _docNamesInAttributes = new();
    private readonly ConcurrentBag<string> _docNamesInRegistration = new();

    public void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(startContext =>
        {
            startContext.RegisterSymbolAction(sc =>
            {
                var typeSymbol = (INamedTypeSymbol)sc.Symbol;
                var ad = typeSymbol.GetAttributes().FirstOrDefault(a => RuleUtils.IsAttribute(a, RuleConstants.AsyncApiAttributeName));
                if (ad != null)
                {
                    string name = "default";
                    if (ad.ConstructorArguments.Length > 0)
                    {
                        name = ad.ConstructorArguments[0].Value?.ToString() ?? "default";
                    }
                    _docNamesInAttributes.Add((name.ToLowerInvariant(), ad.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? typeSymbol.Locations[0]));
                }
            }, SymbolKind.NamedType);

            startContext.RegisterOperationAction(oc =>
            {
                var invocation = (IInvocationOperation)oc.Operation;
                if (invocation.TargetMethod.Name == "AddAsyncApi" && 
                    invocation.TargetMethod.ContainingType.ToDisplayString() == "Bielu.AspNetCore.AsyncApi.Extensions.AsyncApiServiceCollectionExtensions")
                {
                    if (invocation.Arguments.Length > 1) // first is 'services', second is 'documentName' if it exists
                    {
                        var nameArg = invocation.Arguments.FirstOrDefault(a => a.Parameter?.Name == "documentName");
                        if (nameArg != null && nameArg.Value.ConstantValue.HasValue && nameArg.Value.ConstantValue.Value is string name)
                        {
                            _docNamesInRegistration.Add(name.ToLowerInvariant());
                        }
                        else if (invocation.Arguments.Length == 2 && invocation.Arguments[1].Parameter?.Type.TypeKind == TypeKind.Delegate)
                        {
                            _docNamesInRegistration.Add("default");
                        }
                    }
                    else if (invocation.Arguments.Length == 1) // just 'services'
                    {
                        _docNamesInRegistration.Add("default");
                    }
                }
            }, OperationKind.Invocation);

            startContext.RegisterCompilationEndAction(ec =>
            {
                var registered = new HashSet<string>(_docNamesInRegistration);
                if (registered.Count == 0) return;

                foreach (var attr in _docNamesInAttributes)
                {
                    if (!registered.Contains(attr.Name))
                    {
                        ec.ReportDiagnostic(Diagnostic.Create(Descriptor, attr.Location, attr.Name));
                    }
                }
            });
        });
    }
}
