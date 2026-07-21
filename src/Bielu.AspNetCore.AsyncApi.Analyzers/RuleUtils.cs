using Microsoft.CodeAnalysis;

namespace Bielu.AspNetCore.AsyncApi.Analyzers;

internal static class RuleUtils
{
    public static bool IsAttribute(AttributeData ad, string fullName)
    {
        var current = ad.AttributeClass;
        while (current != null)
        {
            if (current.ToDisplayString() == fullName)
                return true;
            current = current.BaseType;
        }
        return false;
    }
}
