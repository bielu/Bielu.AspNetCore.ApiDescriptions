using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Bielu.AspNetCore.Arazzo.Analyzers;

/// <summary>
/// Recognizes the Bielu.AspNetCore.Arazzo API by name, so the analyzer needs no reference to that package.
/// </summary>
internal static class ArazzoSymbols
{
    public const string ExtensionsNamespace = "Bielu.AspNetCore.Arazzo.Extensions";
    public const string OptionsType = "Bielu.AspNetCore.Arazzo.Services.ArazzoOptions";
    public const string WorkflowBuilderType = "Bielu.AspNetCore.Arazzo.Services.ArazzoWorkflowBuilder";
    public const string StepBuilderType = "Bielu.AspNetCore.Arazzo.Services.ArazzoStepBuilder";
    public const string ArazzoIdType = "Bielu.AspNetCore.Arazzo.ArazzoId";
    public const string DefaultDocumentName = "v1";
    public const string SendAction = "send";
    public const string ReceiveAction = "receive";

    public static bool IsOn(IInvocationOperation invocation, string containingType) =>
        invocation.TargetMethod.ContainingType?.ToDisplayString() == containingType;

    public static bool IsAddArazzo(IInvocationOperation invocation) =>
        invocation.TargetMethod.Name == "AddArazzo"
        && invocation.TargetMethod.ContainingNamespace?.ToDisplayString() == ExtensionsNamespace;

    /// <summary>The argument bound to <paramref name="parameterName"/>, or <see langword="null"/>.</summary>
    public static IArgumentOperation? Argument(IInvocationOperation invocation, string parameterName)
    {
        foreach (var argument in invocation.Arguments)
        {
            if (argument.Parameter?.Name == parameterName)
            {
                return argument;
            }
        }

        return null;
    }

    /// <summary>The compile-time constant string bound to <paramref name="parameterName"/>, or <see langword="null"/>.</summary>
    public static string? ConstantString(IInvocationOperation invocation, string parameterName) =>
        Argument(invocation, parameterName)?.Value.ConstantValue is { HasValue: true, Value: string value } ? value : null;

    /// <summary>
    /// The values bound to <paramref name="parameterName"/>: one, or each element of a <c>params</c> array, which
    /// arrives as an array creation whether the caller wrote one or the compiler did.
    /// </summary>
    public static IEnumerable<IOperation> Values(IInvocationOperation invocation, string parameterName) =>
        Argument(invocation, parameterName)?.Value switch
        {
            null => [],
            IArrayCreationOperation { Initializer: { } initializer } => initializer.ElementValues,
            var value => [value],
        };

    /// <summary>
    /// The ids an invocation refers to: the compile-time constants bound to <paramref name="parameterName"/>, plus
    /// the marker type's id for a generic overload.
    /// </summary>
    public static IEnumerable<IdReference> ConstantIds(IInvocationOperation invocation, string parameterName)
    {
        if (MarkerTypeId(invocation) is { } markerId)
        {
            yield return new IdReference(markerId, Bielu.Analyzers.Shared.InvocationLocations.NameLocation(invocation));
        }

        foreach (var value in Values(invocation, parameterName))
        {
            if (value.ConstantValue is { HasValue: true, Value: string id })
            {
                yield return new IdReference(id, value.Syntax.GetLocation());
            }
        }
    }

    /// <summary>
    /// The inline lambda passed as <paramref name="parameterName"/>, or <see langword="null"/> when the delegate
    /// comes from anywhere else (a method group, a variable), whose body this analyzer can't see.
    /// </summary>
    public static IAnonymousFunctionOperation? Lambda(IInvocationOperation invocation, string parameterName)
    {
        var value = Argument(invocation, parameterName)?.Value;
        while (value is IConversionOperation conversion)
        {
            value = conversion.Operand;
        }

        return value is IDelegateCreationOperation { Target: IAnonymousFunctionOperation lambda } ? lambda : null;
    }

    /// <summary>
    /// The invocations on <paramref name="builderType"/> inside <paramref name="lambda"/>, in source order.
    /// </summary>
    public static List<IInvocationOperation> BuilderCalls(IAnonymousFunctionOperation lambda, string builderType) =>
        lambda.Body.Descendants()
            .OfType<IInvocationOperation>()
            .Where(i => IsOn(i, builderType))
            // By the method name, not the invocation: in a chain every call's span starts at the shared receiver.
            .OrderBy(i => Bielu.Analyzers.Shared.InvocationLocations.NameLocation(i).SourceSpan.Start)
            .ToList();

    /// <summary>
    /// Whether <paramref name="lambda"/> only ever calls methods on its builder parameter. When the builder is
    /// passed to a helper, stored, or used conditionally (<c>b?.X()</c>), the lambda's calls aren't the full
    /// configuration, so checks that need the full picture must skip it.
    /// </summary>
    public static bool UsesBuilderOnlyAsReceiver(IAnonymousFunctionOperation lambda, string builderType)
    {
        if (lambda.Symbol.Parameters.Length != 1)
        {
            return false;
        }

        var builder = lambda.Symbol.Parameters[0];
        return lambda.Body.Descendants()
            .OfType<IParameterReferenceOperation>()
            .Where(r => SymbolEqualityComparer.Default.Equals(r.Parameter, builder))
            .All(r => r.Parent is IInvocationOperation call && call.Instance == r && IsOn(call, builderType)
                || r.Parent is IArgumentOperation { Parent: IInvocationOperation extension } && IsOwnExtension(extension));
    }

    // The package's own extension methods on a builder, such as AddOverlay on ArazzoOptions, are known not to
    // declare workflows, steps or sources, unlike an app's own extension method that might.
    private static bool IsOwnExtension(IInvocationOperation invocation) =>
        invocation.TargetMethod.IsExtensionMethod
        && invocation.TargetMethod.ContainingNamespace?.ToDisplayString() is { } ns
        && (ns == "Bielu.AspNetCore.Arazzo" || ns.StartsWith("Bielu.AspNetCore.Arazzo.", System.StringComparison.Ordinal));

    /// <summary>
    /// Whether <paramref name="lambda"/> contains control flow, in which case a call in its body may not run.
    /// </summary>
    public static bool HasBranches(IAnonymousFunctionOperation lambda) =>
        lambda.Body.Descendants().Any(o => o is IConditionalOperation or ISwitchOperation or ISwitchExpressionOperation
            or ILoopOperation or ICoalesceOperation or IConditionalAccessOperation or ITryOperation or IThrowOperation
            or IBranchOperation { IsImplicit: false } or IReturnOperation { IsImplicit: false });

    /// <summary>
    /// The id a generic overload (<c>AddWorkflow&lt;T&gt;</c>, <c>Step&lt;T&gt;</c>, <c>DependsOn&lt;T&gt;</c>,
    /// <c>Workflow&lt;T&gt;</c>) derives from its marker type, or <see langword="null"/> for a non-generic call.
    /// </summary>
    public static string? MarkerTypeId(IInvocationOperation invocation) =>
        invocation.TargetMethod.TypeArguments.Length == 1
        && invocation.TargetMethod.Name is "AddWorkflow" or "Step" or "DependsOn" or "Workflow" or "FromType"
            ? MarkerTypeId(invocation.TargetMethod.TypeArguments[0])
            : null;

    /// <summary>
    /// Mirrors <c>ArazzoId.FromType</c>, which camel-cases <c>Type.Name</c> with
    /// <c>JsonNamingPolicy.CamelCase</c>. A generic type's runtime name carries a backtick and its arity.
    /// </summary>
    public static string MarkerTypeId(ITypeSymbol type)
    {
        var name = type is INamedTypeSymbol { IsGenericType: true } generic
            ? generic.MetadataName
            : type.Name;
        return ToCamelCase(name);
    }

    // The algorithm of System.Text.Json's JsonCamelCaseNamingPolicy, copied rather than referenced so the analyzer
    // doesn't load System.Text.Json into the compiler: lowercase the leading run of uppercase letters, except the
    // last one when it starts the next word ("URLValue" -> "urlValue"). A test pins it to the real policy.
    internal static string ToCamelCase(string name)
    {
        if (name.Length == 0 || !char.IsUpper(name[0]))
        {
            return name;
        }

        var chars = name.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (i == 1 && !char.IsUpper(chars[i]))
            {
                break;
            }

            var hasNext = i + 1 < chars.Length;
            if (i > 0 && hasNext && !char.IsUpper(chars[i + 1]))
            {
                if (chars[i + 1] == ' ')
                {
                    chars[i] = char.ToLowerInvariant(chars[i]);
                }

                break;
            }

            chars[i] = char.ToLowerInvariant(chars[i]);
        }

        return new string(chars);
    }
}
