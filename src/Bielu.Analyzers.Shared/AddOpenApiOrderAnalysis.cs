using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Bielu.Analyzers.Shared;

/// <summary>
/// Shared check behind the rules that report an <c>AddOpenApi()</c> call made after a registration that redirects
/// OpenAPI's build-time document provider (BASYNC010 for <c>AddAsyncApi()</c>, BARAZZO001 for <c>AddArazzo()</c>).
/// </summary>
/// <remarks>
/// Compiled into each analyzer assembly as linked source rather than shipped as its own assembly, because an
/// analyzer can't rely on the compiler loading a second assembly it depends on.
/// </remarks>
internal static class AddOpenApiOrderAnalysis
{
    private const string OpenApiExtensionsType = "Microsoft.Extensions.DependencyInjection.OpenApiServiceCollectionExtensions";

    /// <summary>
    /// Reports <paramref name="descriptor"/> on every <c>AddOpenApi()</c> call that runs after the first invocation
    /// matching <paramref name="isRedirectingRegistration"/> in the analyzed method, with that invocation's method
    /// name as the message argument.
    /// </summary>
    public static void Report(
        OperationBlockAnalysisContext context,
        DiagnosticDescriptor descriptor,
        Func<IInvocationOperation, bool> isRedirectingRegistration)
    {
        var invocations = context.OperationBlocks
            .SelectMany(InEvaluationOrder)
            .ToList();

        var firstRegistration = invocations.FindIndex(i => isRedirectingRegistration(i));
        if (firstRegistration < 0)
        {
            return;
        }

        foreach (var addOpenApi in invocations.Skip(firstRegistration + 1).Where(IsAddOpenApi))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                descriptor,
                InvocationLocations.NameLocation(addOpenApi),
                invocations[firstRegistration].TargetMethod.Name));
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

    private static bool IsAddOpenApi(IInvocationOperation invocation) =>
        invocation.TargetMethod.Name == "AddOpenApi"
        && invocation.TargetMethod.ContainingType.ToDisplayString() == OpenApiExtensionsType;
}
