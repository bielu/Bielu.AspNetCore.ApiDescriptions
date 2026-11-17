using Bielu.Arazzo.Models;

namespace Bielu.AspNetCore.Arazzo;

/// <summary>Builds the Reusable Objects (<c>$components.{kind}.{name}</c>) that point at entries of the Components Object.</summary>
internal static class ArazzoComponentReference
{
    public static ArazzoReferenceable<ArazzoParameter> Parameter(string componentName, string? value)
        => ArazzoReferenceable<ArazzoParameter>.Of(Create("parameters", componentName, value));

    public static ArazzoReferenceable<ArazzoSuccessAction> SuccessAction(string componentName)
        => ArazzoReferenceable<ArazzoSuccessAction>.Of(Create("successActions", componentName, null));

    public static ArazzoReferenceable<ArazzoFailureAction> FailureAction(string componentName)
        => ArazzoReferenceable<ArazzoFailureAction>.Of(Create("failureActions", componentName, null));

    private static ArazzoReusableObject Create(string kind, string componentName, string? value)
    {
        ArazzoIdentifier.ValidateKey(componentName, nameof(componentName));
        return new ArazzoReusableObject { Reference = $"$components.{kind}.{componentName}", Value = value };
    }
}
