using Bielu.Analyzers.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Bielu.AspNetCore.Arazzo.Analyzers.Rules;

/// <summary>
/// Reports an <c>AddOpenApi()</c> call that follows an <c>AddArazzo()</c> call in the same method.
/// </summary>
/// <remarks>
/// <c>AddArazzo()</c> joins the build-time document provider that <c>AddAsyncApi()</c> installs, which redirects
/// OpenAPI's <c>Microsoft.Extensions.ApiDescriptions.IDocumentProvider</c> registration so <c>dotnet getdocument</c>
/// also writes the Arazzo documents. An <c>AddOpenApi()</c> call made afterwards registers OpenAPI's provider again,
/// and if the tool resolves that one, the Arazzo documents are not written. Only calls within one method body are
/// compared; the order across methods can't be known statically.
/// </remarks>
internal static class RuleBARAZZO001AddOpenApiAfterAddArazzo
{
    public static readonly DiagnosticDescriptor Descriptor = new(
        id: "BARAZZO001",
        title: "Register OpenAPI before Arazzo",
        messageFormat: "Call 'AddOpenApi' before '{0}', so build-time generation writes both the OpenAPI and the Arazzo documents",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "AddArazzo() makes build-time generation (dotnet getdocument) serve the OpenAPI documents as well, by redirecting the OpenAPI document provider that AddOpenApi() registered. An AddOpenApi() call made after it registers that provider again, and the Arazzo documents may then be missing from the generated files. Move the AddOpenApi() call above the first AddArazzo() call.");

    public static void AnalyzeOperationBlock(OperationBlockAnalysisContext context) =>
        AddOpenApiOrderAnalysis.Report(context, Descriptor, ArazzoSymbols.IsAddArazzo);
}
