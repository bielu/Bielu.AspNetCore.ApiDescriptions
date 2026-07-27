using Bielu.Arazzo;
using Bielu.Arazzo.Models;
using Bielu.AspNetCore.Arazzo.Services;
using Bielu.AspNetCore.Arazzo.SourceResolvers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bielu.AspNetCore.Arazzo.Validation;

/// <summary>
/// Builds an <see cref="ArazzoWorkspace"/> for one document by resolving its self-wired sources against the
/// live <c>IAsyncApiDocumentProvider</c>/<c>IOpenApiDocumentProvider</c> keyed services registered elsewhere
/// in the app.
/// </summary>
internal sealed class ArazzoWorkspaceFactory(
    [ServiceKey] string documentName,
    IOptionsMonitor<ArazzoOptions> optionsMonitor,
    IServiceProvider serviceProvider)
{
    public async Task<ArazzoWorkspace> CreateAsync(CancellationToken cancellationToken)
    {
        var options = optionsMonitor.Get(documentName);
        var workspace = new ArazzoWorkspace();
        workspace.RegisterResolver(new OpenApiSourceResolver());
        workspace.RegisterResolver(new AsyncApiSourceResolver());

        foreach (var wiring in options.SourceWirings)
        {
            var document = wiring.SourceType switch
            {
                ArazzoSourceDescriptionType.AsyncApi => (object)await serviceProvider
                    .GetRequiredKeyedService<Bielu.AspNetCore.AsyncApi.Services.IAsyncApiDocumentProvider>(
                        wiring.DocumentName)
                    .GetAsyncApiDocumentAsync(cancellationToken),
                ArazzoSourceDescriptionType.OpenApi => await serviceProvider
                    .GetRequiredKeyedService<Microsoft.AspNetCore.OpenApi.IOpenApiDocumentProvider>(wiring.DocumentName)
                    .GetOpenApiDocumentAsync(cancellationToken),
                _ => throw new InvalidOperationException($"Unsupported self-wired source type '{wiring.SourceType}'."),
            };

            workspace.RegisterDocument(wiring.SourceName, wiring.SourceType, document);
        }

        return workspace;
    }
}
