using Bielu.Arazzo.Models;
using Bielu.AspNetCore.Arazzo.Validation;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;

namespace Bielu.AspNetCore.Arazzo.SourceResolvers;

/// <summary>Supplies the OpenAPI documents this app registered with <c>AddOpenApi</c>.</summary>
internal sealed class OpenApiSourceDocumentProvider(IServiceProvider serviceProvider) : IArazzoSourceDocumentProvider
{
    public string SourceType => ArazzoSourceDescriptionType.OpenApi.Value;

    public async ValueTask<object?> GetDocumentAsync(string documentName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(documentName);

        // Registration is checked separately: on ASP.NET Core 11 the keyed lookup alone succeeds for any name.
        var provider = OpenApiDocumentRegistrations.IsRegistered(serviceProvider, documentName)
            ? serviceProvider.GetKeyedService<IOpenApiDocumentProvider>(documentName)
            : null;
        return provider is null ? null : await provider.GetOpenApiDocumentAsync(cancellationToken).ConfigureAwait(false);
    }
}
