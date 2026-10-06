namespace Bielu.AspNetCore.AsyncApi.Attributes.Attributes;

/// <summary>
/// Provides a payload example for an AsyncAPI message.
/// </summary>
public interface IAsyncApiMessageExampleProvider
{
    /// <summary>
    /// Returns the payload example value.
    /// </summary>
    object GetExample();
}