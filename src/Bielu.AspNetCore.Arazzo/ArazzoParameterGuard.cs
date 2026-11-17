using Bielu.Arazzo.Models;

namespace Bielu.AspNetCore.Arazzo;

/// <summary>Argument checks for model objects handed to the fluent builders as-is.</summary>
internal static class ArazzoParameterGuard
{
    /// <summary>
    /// Throws when <paramref name="parameter"/> is null or lacks the name and value its <c>required</c> members
    /// promise, which an object initializer using <c>null!</c> can still leave unset.
    /// </summary>
    public static void Validate(ArazzoParameter parameter, string paramName = "parameter")
    {
        ArgumentNullException.ThrowIfNull(parameter, paramName);
        if (string.IsNullOrEmpty(parameter.Name))
        {
            throw new ArgumentException("The parameter must have a name.", paramName);
        }

        if (parameter.Value is null)
        {
            throw new ArgumentException($"Parameter '{parameter.Name}' must have a value.", paramName);
        }
    }
}
