using Bielu.AspNetCore.AsyncApi.Attributes;
using Bielu.AspNetCore.AsyncApi.Models.Metadata;

namespace Bielu.AspNetCore.AsyncApi.Services;

public interface IAsyncApiMetadataProvider
{
    IEnumerable<AsyncApiTypeMetadata> GetMetadata(string documentName);

    /// <summary>
    /// The <see cref="MessageAttribute"/> placed on payload classes, keyed by the class, for providers that
    /// know them without reflection (the source-generated provider does).
    /// </summary>
    /// <returns>
    /// The class-level attributes, or <see langword="null"/> (the default) when the provider does not supply
    /// them, in which case the document service reads them from the payload type by reflection.
    /// </returns>
    IReadOnlyDictionary<Type, MessageAttribute>? GetPayloadMessageAttributes() => null;
}
