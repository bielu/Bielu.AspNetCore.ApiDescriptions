using Bielu.AspNetCore.ApiDescriptions;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.ApiDescriptions;

/// <summary>
/// The <see cref="IDocumentProvider"/> that <c>dotnet getdocument</c> queries at build time. It lists and writes the
/// documents of every registered <see cref="IBuildTimeDocumentSource"/> and, when the app also calls
/// <c>AddOpenApi()</c>, those of <c>Microsoft.AspNetCore.OpenApi</c> (see <see cref="OpenApiDocumentProviderAdapter"/>).
/// </summary>
/// <remarks>
/// Every document is listed under a build-time name that is unique across all of them, so no two documents are
/// written to the same file. See <see cref="IBuildTimeDocumentSource"/> for how those names are chosen.
/// </remarks>
/// <param name="serviceProvider">The <see cref="IServiceProvider"/> to use.</param>
internal sealed class BuildTimeDocumentProvider(IServiceProvider serviceProvider) : IDocumentProvider
{
    private readonly Lazy<OpenApiDocumentProviderAdapter?> _openApi =
        new(() => OpenApiDocumentProviderAdapter.TryCreate(serviceProvider));

    /// <summary>
    /// Provides the build-time names of all documents: the OpenAPI ones, if any, and those of every source.
    /// </summary>
    /// <exception cref="InvalidOperationException">Two documents end up with the same build-time name.</exception>
    public IEnumerable<string> GetDocumentNames() => GetBuildTimeDocuments().Keys;

    /// <summary>
    /// Serializes the document associated with a given build-time document name to the provided writer.
    /// </summary>
    /// <param name="documentName">The build-time name of the document to resolve.</param>
    /// <param name="writer">A text writer associated with the document to write to.</param>
    public Task GenerateAsync(string documentName, TextWriter writer) =>
        GenerateWithOpenApiVersionAsync(documentName, writer, openApiSpecVersion: null);

    /// <summary>
    /// Serializes the document associated with a given build-time document name, routing OpenAPI document
    /// names to <c>Microsoft.AspNetCore.OpenApi</c> with the <c>OpenApiSpecVersion</c> the caller asked for.
    /// </summary>
    /// <param name="documentName">The build-time name of the document to resolve.</param>
    /// <param name="writer">A text writer associated with the document to write to.</param>
    /// <param name="openApiSpecVersion">The <c>Microsoft.OpenApi.OpenApiSpecVersion</c> to use for an OpenAPI document, if any.</param>
    /// <exception cref="InvalidOperationException">No document has the build-time name <paramref name="documentName"/>.</exception>
    internal Task GenerateWithOpenApiVersionAsync(string documentName, TextWriter writer, object? openApiSpecVersion)
    {
        var documents = GetBuildTimeDocuments();
        if (!documents.TryGetValue(documentName, out var document))
        {
            throw new InvalidOperationException(
                $"No document has the build-time name '{documentName}'. Known names: {string.Join(", ", documents.Keys.Select(name => $"'{name}'"))}.");
        }

        return document.Source is null
            ? _openApi.Value!.GenerateAsync(document.Name, writer, openApiSpecVersion)
            : document.Source.GenerateAsync(document.Name, writer);
    }

    /// <summary>
    /// Maps every document's build-time name to its document. OpenAPI names are settled first and never change;
    /// sources follow in ascending <see cref="IBuildTimeDocumentSource.Order"/>. Sources and documents are visited
    /// in a fixed order, so registration order never changes the result.
    /// </summary>
    /// <exception cref="InvalidOperationException">Two documents end up with the same build-time name.</exception>
    private Dictionary<string, NamedDocument> GetBuildTimeDocuments()
    {
        var names = new Dictionary<string, NamedDocument>(StringComparer.OrdinalIgnoreCase);
        foreach (var openApiName in _openApi.Value?.GetDocumentNames() ?? [])
        {
            names.TryAdd(openApiName, new NamedDocument(Source: null, openApiName));
        }

        var tiers = serviceProvider.GetServices<IBuildTimeDocumentSource>()
            .GroupBy(source => source.Order)
            .OrderBy(tier => tier.Key);
        foreach (var tier in tiers)
        {
            AddTier(names, tier);
        }

        return names;
    }

    /// <summary>
    /// Names the documents of sources sharing one <see cref="IBuildTimeDocumentSource.Order"/>. A document keeps its
    /// own name unless an earlier document or another document of the same tier uses it, in which case it gets
    /// its source's prefix. Only earlier tiers count as taken, so the order documents are visited in within the
    /// tier does not matter.
    /// </summary>
    private static void AddTier(Dictionary<string, NamedDocument> names, IEnumerable<IBuildTimeDocumentSource> tier)
    {
        var documents = tier
            .OrderBy(source => source.NamePrefix, StringComparer.Ordinal)
            .SelectMany(source => source.GetDocuments()
                .OrderBy(document => document.Name, StringComparer.OrdinalIgnoreCase)
                .Select(document => (Source: source, Document: document)))
            .ToList();

        var sharedNames = documents
            .GroupBy(entry => entry.Document.Name, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var takenNames = names.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var (source, document) in documents)
        {
            var buildTimeName = !string.IsNullOrWhiteSpace(document.BuildTimeName)
                ? document.BuildTimeName
                : takenNames.Contains(document.Name) || sharedNames.Contains(document.Name)
                    ? source.NamePrefix + document.Name
                    : document.Name;

            // A shared name would make one document silently replace the other in the generated files.
            if (names.TryGetValue(buildTimeName, out var owner))
            {
                throw new InvalidOperationException(owner.Source is null
                    ? $"{source.Kind} document '{document.Name}' uses the build-time name '{buildTimeName}', which is also the name of an OpenAPI document. " +
                      "Set a different BuildTimeDocumentName on it."
                    : $"{owner.Source.Kind} document '{owner.Name}' and {source.Kind} document '{document.Name}' both use the build-time name '{buildTimeName}'. " +
                      "Set a different BuildTimeDocumentName on one of them.");
            }

            names.Add(buildTimeName, new NamedDocument(source, document.Name));
        }
    }

    /// <summary>A document under its build-time name; <see cref="Source"/> is <see langword="null"/> for OpenAPI.</summary>
    private sealed record NamedDocument(IBuildTimeDocumentSource? Source, string Name);
}
