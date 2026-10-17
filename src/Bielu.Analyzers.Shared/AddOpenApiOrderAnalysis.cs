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
        // A lambda or local function body runs later, if at all, so each is a sequence of its own rather than
        // part of the method body it's declared in.
        var bodies = new Queue<IOperation>(context.OperationBlocks);
        while (bodies.Count > 0)
        {
            ReportSequence(context, descriptor, isRedirectingRegistration, InEvaluationOrder(bodies.Dequeue(), bodies).ToList());
        }
    }

    private static void ReportSequence(
        OperationBlockAnalysisContext context,
        DiagnosticDescriptor descriptor,
        Func<IInvocationOperation, bool> isRedirectingRegistration,
        List<IInvocationOperation> invocations)
    {
        var firstRegistration = invocations.FindIndex(i => isRedirectingRegistration(i));
        if (firstRegistration < 0)
        {
            return;
        }

        var registration = invocations[firstRegistration];
        foreach (var addOpenApi in invocations.Skip(firstRegistration + 1).Where(IsAddOpenApi))
        {
            if (AreMutuallyExclusive(registration, addOpenApi))
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(
                descriptor,
                InvocationLocations.NameLocation(addOpenApi),
                registration.TargetMethod.Name));
        }
    }

    // The order the invocations run in, which isn't their order in the source: a call's receiver and
    // arguments run before the call itself, so in both services.AddAsyncApi().AddOpenApi() and
    // AddOpenApi(services.AddAsyncApi()), AddAsyncApi runs first. A post-order walk yields exactly that.
    // Nested lambda and local function bodies are not walked but handed to deferredBodies instead.
    private static IEnumerable<IInvocationOperation> InEvaluationOrder(IOperation operation, Queue<IOperation> deferredBodies)
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

            if (current is IAnonymousFunctionOperation lambda)
            {
                deferredBodies.Enqueue(lambda.Body);
                continue;
            }

            if (current is ILocalFunctionOperation localFunction)
            {
                if (localFunction.Body is { } body)
                {
                    deferredBodies.Enqueue(body);
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

    // Whether two calls sit in different branches of the same if/else, ?: or switch, so they never both run.
    private static bool AreMutuallyExclusive(IOperation first, IOperation second)
    {
        var firstBranches = Branches(first).ToList();
        return Branches(second).Any(b => firstBranches.Any(f => f.Branching == b.Branching && f.Branch != b.Branch));
    }

    private static IEnumerable<(IOperation Branching, IOperation Branch)> Branches(IOperation operation)
    {
        for (var child = operation; child.Parent is { } parent; child = parent)
        {
            var isBranch = parent switch
            {
                IConditionalOperation conditional => child == conditional.WhenTrue || child == conditional.WhenFalse,
                ISwitchOperation => child is ISwitchCaseOperation,
                ISwitchExpressionOperation => child is ISwitchExpressionArmOperation,
                _ => false,
            };

            if (isBranch)
            {
                yield return (parent, child);
            }
        }
    }

    private static bool IsAddOpenApi(IInvocationOperation invocation) =>
        invocation.TargetMethod.Name == "AddOpenApi"
        && invocation.TargetMethod.ContainingType.ToDisplayString() == OpenApiExtensionsType;
}
