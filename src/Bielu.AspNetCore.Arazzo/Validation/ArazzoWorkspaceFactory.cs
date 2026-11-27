using Bielu.Arazzo;
using Bielu.AspNetCore.Arazzo.Services;
using Bielu.AspNetCore.Arazzo.SourceResolvers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bielu.AspNetCore.Arazzo.Validation;

/// <summary>
/// Builds an <see cref="ArazzoWorkspace"/> for one document by asking the registered
/// <see cref="IArazzoSourceDocumentProvider"/>s for the document behind each self-wired source.
/// </summary>
internal sealed class ArazzoWorkspaceFactory(
    [ServiceKey] string documentName,
    IOptionsMonitor<ArazzoOptions> optionsMonitor,
    IEnumerable<IArazzoSourceResolver> resolvers,
    IEnumerable<IArazzoSourceDocumentProvider> documentProviders)
{
    /// <summary>
    /// Builds the workspace for this document. A source no provider can supply a document for (a missing or
    /// mistyped document name, or a source type with no provider) is skipped rather than throwing; instead a
    /// contextual message naming the Arazzo document, source-description name, source type, and requested document
    /// name is appended to <paramref name="errors"/>, so every misconfigured source in the document is reported in
    /// one pass. Such a source has no document in the returned workspace, so the reference validator skips the steps
    /// that target it rather than reporting each one as a separate "did not resolve" failure.
    /// </summary>
    public async Task<ArazzoWorkspace> CreateAsync(
        List<string> errors, CancellationToken cancellationToken)
    {
        var options = optionsMonitor.Get(documentName);
        var workspace = new ArazzoWorkspace();
        foreach (var resolver in resolvers)
        {
            workspace.RegisterResolver(resolver);
        }

        // Newest first: a provider added after AddArazzo is asked before the built-in one for its type.
        var providersByType = documentProviders.Reverse()
            .ToLookup(provider => provider.SourceType, StringComparer.Ordinal);

        foreach (var wiring in options.SourceWirings)
        {
            var sourceType = wiring.SourceType.Value;
            var document = await GetDocumentAsync(providersByType[sourceType], wiring.DocumentName, cancellationToken);
            if (document is not null)
            {
                workspace.RegisterDocument(wiring.SourceName, sourceType, document);
                continue;
            }

            errors.Add(providersByType.Contains(sourceType)
                ? $"{documentName}: source '{wiring.SourceName}' ({sourceType}) references document " +
                  $"'{wiring.DocumentName}', but no {sourceType} document of that name is registered" +
                  $"{RegistrationHint(sourceType, wiring.DocumentName)}."
                : $"{documentName}: source '{wiring.SourceName}' is self-wired as type '{sourceType}', but no " +
                  $"{nameof(IArazzoSourceDocumentProvider)} is registered for that type.");
        }

        return workspace;
    }

    private static async Task<object?> GetDocumentAsync(
        IEnumerable<IArazzoSourceDocumentProvider> providers, string sourceDocumentName,
        CancellationToken cancellationToken)
    {
        foreach (var provider in providers)
        {
            if (await provider.GetDocumentAsync(sourceDocumentName, cancellationToken) is { } document)
            {
                return document;
            }
        }

        return null;
    }

    /// <summary>For the two built-in types, names the call that registers the missing document.</summary>
    private static string RegistrationHint(string sourceType, string sourceDocumentName) => sourceType switch
    {
        "asyncapi" => $" (no AddAsyncApi(\"{sourceDocumentName}\", ...))",
        "openapi" => $" (no AddOpenApi(\"{sourceDocumentName}\", ...))",
        _ => string.Empty,
    };
}
