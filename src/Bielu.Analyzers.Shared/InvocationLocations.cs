using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Bielu.Analyzers.Shared;

internal static class InvocationLocations
{
    /// <summary>
    /// The location of the called method's name (<c>AddOpenApi</c> in <c>services.AddOpenApi()</c>), so a squiggle
    /// marks the call rather than its whole receiver chain. Falls back to the whole invocation.
    /// </summary>
    public static Location NameLocation(IInvocationOperation invocation) =>
        invocation.Syntax switch
        {
            InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax memberAccess } => memberAccess.Name.GetLocation(),
            InvocationExpressionSyntax { Expression: SimpleNameSyntax name } => name.GetLocation(),
            _ => invocation.Syntax.GetLocation(),
        };
}
