using System.Collections.Immutable;
using Bielu.AspNetCore.AsyncApi.Analyzers.Rules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Bielu.AspNetCore.AsyncApi.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AsyncApiAttributeAnalyzer : DiagnosticAnalyzer
{
    private readonly RuleBASYNC004UnusedDocumentName _rule004 = new();

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
        RuleBASYNC001MissingAsyncApi.Descriptor,
        RuleBASYNC002MissingChannel.Descriptor,
        RuleBASYNC003DuplicateName.Descriptor,
        RuleBASYNC004UnusedDocumentName.Descriptor,
        RuleBASYNC005InvalidPayloadType.Descriptor,
        RuleBASYNC006InvalidJsonExample.Descriptor,
        RuleBASYNC007MissingParameterlessConstructor.Descriptor,
        RuleBASYNC008DiscouragedCharacters.Descriptor,
        RuleBASYNC009MissingDocumentation.Descriptor);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        _rule004.Initialize(context);

        context.RegisterSymbolAction(sc =>
        {
            RuleBASYNC001MissingAsyncApi.AnalyzeNamedType(sc);
            RuleBASYNC003DuplicateName.AnalyzeNamedType(sc);
            RuleBASYNC005InvalidPayloadType.AnalyzeNamedType(sc);
            RuleBASYNC007MissingParameterlessConstructor.AnalyzeNamedType(sc);
            RuleBASYNC008DiscouragedCharacters.AnalyzeNamedType(sc);
        }, SymbolKind.NamedType);

        context.RegisterSymbolAction(sc =>
        {
            RuleBASYNC001MissingAsyncApi.AnalyzeMethod(sc);
            RuleBASYNC002MissingChannel.AnalyzeMethod(sc);
            RuleBASYNC003DuplicateName.AnalyzeMethod(sc);
            RuleBASYNC005InvalidPayloadType.AnalyzeMethod(sc);
            RuleBASYNC006InvalidJsonExample.AnalyzeMethod(sc);
            RuleBASYNC007MissingParameterlessConstructor.AnalyzeMethod(sc);
            RuleBASYNC008DiscouragedCharacters.AnalyzeMethod(sc);
            RuleBASYNC009MissingDocumentation.AnalyzeMethod(sc);
        }, SymbolKind.Method);
    }
}
