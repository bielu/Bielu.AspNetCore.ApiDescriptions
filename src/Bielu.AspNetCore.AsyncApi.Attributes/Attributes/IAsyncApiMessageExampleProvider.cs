namespace Bielu.AspNetCore.AsyncApi.Attributes.Attributes;

/// <summary>
/// Provides an example for an AsyncAPI message.
/// </summary>
public interface IAsyncApiMessageExampleProvider
{
    /// <summary>
    /// Returns the example value.
    /// </summary>
    object GetExample();
}
