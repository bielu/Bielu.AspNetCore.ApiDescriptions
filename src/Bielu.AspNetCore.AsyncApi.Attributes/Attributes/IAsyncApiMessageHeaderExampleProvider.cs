namespace Bielu.AspNetCore.AsyncApi.Attributes.Attributes;

/// <summary>
/// Provides a header example for an AsyncAPI message.
/// </summary>
public interface IAsyncApiMessageHeaderExampleProvider
{
    /// <summary>
    /// Returns the header example value. Must serialize to a JSON object, since AsyncAPI message
    /// headers are a map of header name to example value.
    /// </summary>
    object GetHeaderExample();
}
