using Bielu.Arazzo;
using Bielu.Arazzo.Models;
using Bielu.Arazzo.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bielu.AspNetCore.Arazzo.Services;

/// <summary>Builds and caches the <see cref="ArazzoDocument"/> for one keyed document name.</summary>
internal sealed class ArazzoDocumentService([ServiceKey] string documentName, IOptionsMonitor<ArazzoOptions> optionsMonitor) : IArazzoDocumentProvider
{
    private ArazzoDocument? _cached;

    public Task<ArazzoDocument> GetArazzoDocumentAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _cached ??= Build(optionsMonitor.Get(documentName));
        return Task.FromResult(_cached);
    }

    private static ArazzoDocument Build(ArazzoOptions options)
    {
        if (options.Info is null)
        {
            throw new InvalidOperationException(
                $"Arazzo document '{options.DocumentName}' has no Info; call WithInfo(...) when configuring AddArazzo.");
        }

        var document = new ArazzoDocument
        {
            Arazzo = ArazzoVersion.V1_1.ToVersionString(),
            Info = options.Info,
            SourceDescriptions = [.. options.SourceDescriptions],
            Workflows = [.. options.Workflows],
        };

        var errors = ArazzoValidator.Validate(document).Where(e => !e.IsWarning).ToList();
        if (errors.Count > 0)
        {
            throw new ArazzoDocumentValidationException(options.DocumentName, errors);
        }

        return document;
    }
}
