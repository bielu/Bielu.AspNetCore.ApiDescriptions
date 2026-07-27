using Bielu.Arazzo.Models;

namespace Bielu.AspNetCore.Arazzo.Services;

/// <summary>Represents a provider for Arazzo documents that can be used by consumers to retrieve the generated document at runtime.</summary>
public interface IArazzoDocumentProvider
{
    /// <summary>Gets the Arazzo document, building and caching it on first use.</summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task<ArazzoDocument> GetArazzoDocumentAsync(CancellationToken cancellationToken = default);
}
