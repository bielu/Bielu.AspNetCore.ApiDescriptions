namespace Bielu.AspNetCore.AsyncApi.Attributes;

/// <summary>
/// Marks a class or interface as containing asyncapi channels.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
public sealed class AsyncApiAttribute(string? documentName = null) : Attribute
{
    public string? DocumentName { get; } = documentName;
}
