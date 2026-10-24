namespace Bielu.AspNetCore.AsyncApi.Attributes;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true)]
public class ChannelParameterAttribute(string name, Type type) : Attribute
{
    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));

    public Type Type { get; } = type ?? throw new ArgumentNullException(nameof(type));

    public string? Description { get; set; }

    public string? Location { get; set; }
}
