using Bielu.Arazzo;
using Bielu.Arazzo.Models;
using Bielu.Arazzo.Validation;
using Bielu.AspNetCore.Arazzo.Transformers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Bielu.AspNetCore.Arazzo.Services;

/// <summary>Builds and caches the <see cref="ArazzoDocument"/> for one keyed document name.</summary>
internal sealed class ArazzoDocumentService(
    [ServiceKey] string documentName,
    IOptionsMonitor<ArazzoOptions> optionsMonitor,
    IServiceProvider services) : IArazzoDocumentProvider
{
    private readonly SemaphoreSlim _buildLock = new(1, 1);
    private ArazzoDocument? _cached;

    public async Task<ArazzoDocument> GetArazzoDocumentAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_cached is not null)
        {
            return _cached;
        }

        await _buildLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return _cached ??= await BuildAsync(optionsMonitor.Get(documentName), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _buildLock.Release();
        }
    }

    private async Task<ArazzoDocument> BuildAsync(ArazzoOptions options, CancellationToken cancellationToken)
    {
        // Without WithInfo, the info defaults as an AsyncAPI document's does: "{application} | {document}", 1.0.0.
        var info = options.Info is { } configuredInfo ? ArazzoModelCopy.Copy(configuredInfo) : new ArazzoInfo
        {
            Title = services.GetService<IHostEnvironment>() is { } environment
                ? $"{environment.ApplicationName} | {options.DocumentName}"
                : options.DocumentName,
            Version = ArazzoDefaults.DefaultInfoVersion,
        };

        // Each build gets its own copy of the configured model, so a transformer that changed it and then threw
        // (or was cancelled) cannot leave its change in the options for the next attempt.
        var document = new ArazzoDocument
        {
            Arazzo = options.Version.ToVersionString(),
            Self = options.Self,
            Info = info,
            SourceDescriptions = [.. options.SourceDescriptions.Select(ArazzoModelCopy.Copy)],
            Workflows = [.. options.Workflows.Select(ArazzoModelCopy.Copy)],
            Components = options.Components is { } components ? ArazzoModelCopy.Copy(components) : null,
            Extensions = ArazzoModelCopy.CopyExtensions(options.Extensions),
        };

        // Transformers run on the object model before validation, so what they add is validated like the rest.
        var context = new ArazzoDocumentTransformerContext { DocumentName = options.DocumentName, ApplicationServices = services };
        foreach (var transformer in options.DocumentTransformers)
        {
            await transformer.TransformAsync(document, context, cancellationToken).ConfigureAwait(false);
        }

        var errors = ArazzoValidator.Validate(document).Where(e => !e.IsWarning).ToList();
        if (errors.Count > 0)
        {
            throw new ArazzoDocumentValidationException(options.DocumentName, errors);
        }

        return document;
    }
}
