using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Bielu.AspNetCore.AsyncApi.Analyzers.Rules;

/// <summary>
/// Reports an <c>AddOpenApi()</c> call that follows an <c>AddAsyncApi()</c> or <c>AddArazzo()</c> call in the same method.
/// </summary>
/// <remarks>
/// Both libraries register an internal <c>Microsoft.Extensions.ApiDescriptions.IDocumentProvider</c> for build-time
/// generation, and <c>dotnet getdocument</c> asks only one of them. <c>AddAsyncApi()</c> (and <c>AddArazzo()</c>, which
/// joins the same provider) makes either one answer for
/// both libraries, but it can only redirect an OpenAPI registration that already exists. An <c>AddOpenApi()</c> call
/// made afterwards registers OpenAPI's provider again, so if the tool resolves that one, the AsyncAPI documents are
/// not written. Only calls within one method body are compared; the order across methods can't be known statically.
/// </remarks>
internal static class RuleBASYNC010AddOpenApiAfterAddAsyncApi
{
    private const string OpenApiExtensionsType = "Microsoft.Extensions.DependencyInjection.OpenApiServiceCollectionExtensions";
    private const string AsyncApiNamespace = "Bielu.AspNetCore.AsyncApi";
    private const string ArazzoExtensionsNamespace = "Bielu.AspNetCore.Arazzo.Extensions";

    public static readonly DiagnosticDescriptor Descriptor = new(
        id: "BASYNC010",
        title: "Register OpenAPI before AsyncAPI and Arazzo",
        messageFormat: "Call 'AddOpenApi' before '{0}', so build-time generation writes the OpenAPI document together with the AsyncAPI and Arazzo ones",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "AddAsyncApi() and AddArazzo() make build-time generation (dotnet getdocument) serve the OpenAPI documents as well, by redirecting the OpenAPI document provider that AddOpenApi() registered. An AddOpenApi() call made after them registers that provider again, and the AsyncAPI and Arazzo documents may then be missing from the generated files. Move the AddOpenApi() call above the first AddAsyncApi() or AddArazzo() call.");

    public static void AnalyzeOperationBlock(OperationBlockAnalysisContext context)
    {
        var invocations = context.OperationBlocks
            .SelectMany(InEvaluationOrder)
            .ToList();

        var firstJoiningCall = invocations.FindIndex(IsBuildTimeProviderRegistration);
        if (firstJoiningCall < 0)
        {
            return;
        }

        foreach (var addOpenApi in invocations.Skip(firstJoiningCall + 1).Where(IsAddOpenApi))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Descriptor,
                GetNameLocation(addOpenApi),
                invocations[firstJoiningCall].TargetMethod.Name));
        }
    }

    // The order the invocations run in, which isn't their order in the source: a call's receiver and
    // arguments run before the call itself, so in both services.AddAsyncApi().AddOpenApi() and
    // AddOpenApi(services.AddAsyncApi()), AddAsyncApi runs first. A post-order walk yields exactly that.
    private static IEnumerable<IInvocationOperation> InEvaluationOrder(IOperation operation)
    {
        var pending = new Stack<(IOperation Operation, bool ChildrenVisited)>();
        pending.Push((operation, false));
        while (pending.Count > 0)
        {
            var (current, childrenVisited) = pending.Pop();
            if (childrenVisited)
            {
                if (current is IInvocationOperation invocation)
                {
                    yield return invocation;
                }

                continue;
            }

            pending.Push((current, true));
            foreach (var child in current.ChildOperations.Reverse())
            {
                pending.Push((child, false));
            }
        }
    }

    // AddAsyncApi(...), AddAsyncApiForApiVersions(...) and AddArazzo(...) all join the build-time document provider.
    private static bool IsBuildTimeProviderRegistration(IInvocationOperation invocation) =>
        invocation.TargetMethod.ContainingNamespace?.ToDisplayString() is { } ns
        && (invocation.TargetMethod.Name is "AddAsyncApi" or "AddAsyncApiForApiVersions"
                && (ns == AsyncApiNamespace || ns.StartsWith(AsyncApiNamespace + ".", System.StringComparison.Ordinal))
            || invocation.TargetMethod.Name == "AddArazzo" && ns == ArazzoExtensionsNamespace);

    private static bool IsAddOpenApi(IInvocationOperation invocation) =>
        invocation.TargetMethod.Name == "AddOpenApi"
        && invocation.TargetMethod.ContainingType.ToDisplayString() == OpenApiExtensionsType;

    private static Location GetNameLocation(IInvocationOperation invocation) =>
        invocation.Syntax is Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax
        {
            Expression: Microsoft.CodeAnalysis.CSharp.Syntax.MemberAccessExpressionSyntax memberAccess
        }
            ? memberAccess.Name.GetLocation()
            : invocation.Syntax.GetLocation();
}
