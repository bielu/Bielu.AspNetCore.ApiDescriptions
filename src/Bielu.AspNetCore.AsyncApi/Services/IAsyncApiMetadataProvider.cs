using Bielu.AspNetCore.AsyncApi.Models.Metadata;

namespace Bielu.AspNetCore.AsyncApi.Services;

public interface IAsyncApiMetadataProvider
{
    IEnumerable<AsyncApiTypeMetadata> GetMetadata(string documentName);
}
