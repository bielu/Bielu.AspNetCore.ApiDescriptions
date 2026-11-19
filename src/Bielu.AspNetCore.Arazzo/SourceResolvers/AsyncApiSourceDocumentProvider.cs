using Bielu.Arazzo.Models;
using Bielu.AspNetCore.AsyncApi.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Bielu.AspNetCore.Arazzo.SourceResolvers;

/// <summary>Supplies the AsyncAPI documents this app registered with <c>AddAsyncApi</c>.</summary>
internal sealed class AsyncApiSourceDocumentProvider(IServiceProvider serviceProvider) : IArazzoSourceDocumentProvider
{
    public string SourceType => ArazzoSourceDescriptionType.AsyncApi.Value;

    public async ValueTask<object?> GetDocumentAsync(string documentName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(documentName);

        var provider = serviceProvider.GetKeyedService<IAsyncApiDocumentProvider>(documentName);
        return provider is null ? null : await provider.GetAsyncApiDocumentAsync(cancellationToken).ConfigureAwait(false);
    }
}
