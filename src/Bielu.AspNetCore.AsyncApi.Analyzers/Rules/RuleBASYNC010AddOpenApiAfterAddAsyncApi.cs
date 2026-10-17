using Bielu.Analyzers.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Bielu.AspNetCore.AsyncApi.Analyzers.Rules;

/// <summary>
/// Reports an <c>AddOpenApi()</c> call that follows an <c>AddAsyncApi()</c> call in the same method.
/// </summary>
/// <remarks>
/// Both libraries register an internal <c>Microsoft.Extensions.ApiDescriptions.IDocumentProvider</c> for build-time
/// generation, and <c>dotnet getdocument</c> asks only one of them. <c>AddAsyncApi()</c> makes either one answer for
/// both libraries, but it can only redirect an OpenAPI registration that already exists. An <c>AddOpenApi()</c> call
/// made afterwards registers OpenAPI's provider again, so if the tool resolves that one, the AsyncAPI documents are
/// not written. Only calls within one method body are compared; the order across methods can't be known statically.
/// </remarks>
internal static class RuleBASYNC010AddOpenApiAfterAddAsyncApi
{
    private const string AsyncApiNamespace = "Bielu.AspNetCore.AsyncApi";

    public static readonly DiagnosticDescriptor Descriptor = new(
        id: "BASYNC010",
        title: "Register OpenAPI before AsyncAPI",
        messageFormat: "Call 'AddOpenApi' before '{0}', so build-time generation writes both the OpenAPI and the AsyncAPI documents",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "AddAsyncApi() makes build-time generation (dotnet getdocument) serve the OpenAPI documents as well, by redirecting the OpenAPI document provider that AddOpenApi() registered. An AddOpenApi() call made after it registers that provider again, and the AsyncAPI documents may then be missing from the generated files. Move the AddOpenApi() call above the first AddAsyncApi() call.");

    public static void AnalyzeOperationBlock(OperationBlockAnalysisContext context) =>
        AddOpenApiOrderAnalysis.Report(context, Descriptor, IsAddAsyncApi);

    // AddAsyncApi(...) and AddAsyncApiForApiVersions(...) both register the build-time document provider.
    private static bool IsAddAsyncApi(IInvocationOperation invocation) =>
        invocation.TargetMethod.Name is "AddAsyncApi" or "AddAsyncApiForApiVersions"
        && invocation.TargetMethod.ContainingNamespace?.ToDisplayString() is { } ns
        && (ns == AsyncApiNamespace || ns.StartsWith(AsyncApiNamespace + ".", System.StringComparison.Ordinal));
}
