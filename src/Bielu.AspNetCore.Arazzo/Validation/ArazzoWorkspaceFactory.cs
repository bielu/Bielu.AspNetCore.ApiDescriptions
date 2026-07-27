using Bielu.Arazzo;
using Bielu.Arazzo.Models;
using Bielu.AspNetCore.Arazzo.Services;
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
    IServiceProvider serviceProvider,
    IEnumerable<IArazzoSourceResolver> resolvers)
{
    /// <summary>
    /// Builds the workspace for this document. A source wiring whose upstream document provider isn't
    /// registered (a missing or mistyped <c>AddAsyncApiSource</c>/<c>AddOpenApiSource</c> document name) is
    /// skipped rather than throwing a raw DI exception; instead a contextual message naming the Arazzo
    /// document, source-description name, source type, and requested document name is appended to
    /// <paramref name="errors"/>, so every misconfigured source in the document is reported in one pass.
    /// The returned <c>FailedSourceNames</c> lets the caller skip re-reporting steps that target one of
    /// these sources as a separate "did not resolve" failure — the root-cause message above already covers
    /// them.
    /// </summary>
    public async Task<(ArazzoWorkspace Workspace, HashSet<string> FailedSourceNames)> CreateAsync(
        List<string> errors, CancellationToken cancellationToken)
    {
        var options = optionsMonitor.Get(documentName);
        var workspace = new ArazzoWorkspace();
        foreach (var resolver in resolvers)
        {
            workspace.RegisterResolver(resolver);
        }

        var failedSourceNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var wiring in options.SourceWirings)
        {
            var document = wiring.SourceType switch
            {
                ArazzoSourceDescriptionType.AsyncApi => await ResolveAsyncApiDocumentAsync(wiring, errors,
                    cancellationToken),
                ArazzoSourceDescriptionType.OpenApi => await ResolveOpenApiDocumentAsync(wiring, errors,
                    cancellationToken),
                _ => throw new InvalidOperationException($"Unsupported self-wired source type '{wiring.SourceType}'."),
            };

            if (document is not null)
            {
                workspace.RegisterDocument(wiring.SourceName, wiring.SourceType, document);
            }
            else
            {
                failedSourceNames.Add(wiring.SourceName);
            }
        }

        return (workspace, failedSourceNames);
    }

    private async Task<object?> ResolveAsyncApiDocumentAsync(
        ArazzoOptions.SourceWiring wiring, List<string> errors, CancellationToken cancellationToken)
    {
        var provider = serviceProvider
            .GetKeyedService<Bielu.AspNetCore.AsyncApi.Services.IAsyncApiDocumentProvider>(wiring.DocumentName);
        if (provider is null)
        {
            errors.Add(MissingProviderError(wiring, "AddAsyncApi"));
            return null;
        }

        return await provider.GetAsyncApiDocumentAsync(cancellationToken);
    }

    private async Task<object?> ResolveOpenApiDocumentAsync(
        ArazzoOptions.SourceWiring wiring, List<string> errors, CancellationToken cancellationToken)
    {
        var provider = serviceProvider
            .GetKeyedService<Microsoft.AspNetCore.OpenApi.IOpenApiDocumentProvider>(wiring.DocumentName);
        if (provider is null)
        {
            errors.Add(MissingProviderError(wiring, "AddOpenApi"));
            return null;
        }

        return await provider.GetOpenApiDocumentAsync(cancellationToken);
    }

    private string MissingProviderError(ArazzoOptions.SourceWiring wiring, string expectedRegistrationMethod) =>
        $"{documentName}: source '{wiring.SourceName}' ({wiring.SourceType}) references document " +
        $"'{wiring.DocumentName}', but no {expectedRegistrationMethod}(\"{wiring.DocumentName}\", ...) was " +
        "registered for it.";
}
