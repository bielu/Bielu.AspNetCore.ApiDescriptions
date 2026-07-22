namespace Bielu.AspNetCore.AsyncApi.Attributes.Attributes;

/// <summary>
/// Adds an example to an AsyncAPI message.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class MessageExampleAttribute : Attribute
{
    /// <summary>
    /// A machine-friendly name for the example.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// A short summary of what the example is about.
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// A raw JSON string to use as the example.
    /// </summary>
    public string? Json { get; set; }

    /// <summary>
    /// The type of a provider that implements <see cref="IAsyncApiMessageExampleProvider"/>.
    /// </summary>
    public Type? ProviderType { get; set; }
}
