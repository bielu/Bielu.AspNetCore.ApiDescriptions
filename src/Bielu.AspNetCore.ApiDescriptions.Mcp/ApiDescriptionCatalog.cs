using Bielu.AspNetCore.ApiDescriptions;
using Microsoft.Extensions.ApiDescriptions;
using Microsoft.Extensions.DependencyInjection;

namespace Bielu.AspNetCore.ApiDescriptions.Mcp;

/// <summary>An API description document the app serves, as the MCP server exposes it.</summary>
/// <param name="Kind">The kind of document, for example <c>AsyncAPI</c>.</param>
/// <param name="Name">The document's name, for example <c>v1</c>.</param>
internal sealed record ApiDescriptionDocument(string Kind, string Name)
{
    internal const string UriScheme = "apidescription";

    /// <summary>The document's resource URI: <c>apidescription://{kind}/{name}</c>, with the kind in lower case.</summary>
    public string Uri => $"{UriScheme}://{Kind.ToLowerInvariant()}/{System.Uri.EscapeDataString(Name)}";

    public string MimeType => Kind.ToLowerInvariant() switch
    {
        "asyncapi" => "application/vnd.aai.asyncapi+json",
        "openapi" => "application/vnd.oai.openapi+json",
        "arazzo" => "application/vnd.oai.workflows+json",
        _ => "application/json",
    };
}

/// <summary>
/// Lists and writes the app's API description documents: those of every <see cref="IBuildTimeDocumentSource"/>
/// (AsyncAPI, Arazzo, …) and, when the app uses <c>Microsoft.AspNetCore.OpenApi</c>, its OpenAPI documents. The
/// documents are written as build-time generation writes them, so serialized-document transformers such as overlays
/// have been applied.
/// </summary>
internal sealed class ApiDescriptionCatalog(IServiceProvider services)
{
    internal const string OpenApiKind = "OpenAPI";

    private readonly Lazy<OpenApiDocumentProviderAdapter?> _openApi = new(() => OpenApiDocumentProviderAdapter.TryCreate(services));

    public IReadOnlyList<ApiDescriptionDocument> GetDocuments()
    {
        var documents = new List<ApiDescriptionDocument>();
        documents.AddRange((_openApi.Value?.GetDocumentNames() ?? [])
            .Order(StringComparer.Ordinal)
            .Select(name => new ApiDescriptionDocument(OpenApiKind, name)));

        foreach (var source in Sources().OrderBy(source => source.Order).ThenBy(source => source.Kind, StringComparer.Ordinal))
        {
            documents.AddRange(source.GetDocuments()
                .Select(document => document.Name)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .Select(name => new ApiDescriptionDocument(source.Kind, name)));
        }

        return documents;
    }

    /// <summary>Finds a document by kind and name, ignoring case, as agents rarely reproduce it exactly.</summary>
    public ApiDescriptionDocument? Find(string kind, string name) => GetDocuments().FirstOrDefault(document =>
        string.Equals(document.Kind, kind, StringComparison.OrdinalIgnoreCase)
        && string.Equals(document.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Finds a document by its resource URI.</summary>
    public ApiDescriptionDocument? FindByUri(string uri) =>
        GetDocuments().FirstOrDefault(document => string.Equals(document.Uri, uri, StringComparison.OrdinalIgnoreCase));

    public async Task<string> GenerateAsync(ApiDescriptionDocument document)
    {
        await using var writer = new StringWriter();
        if (document.Kind == OpenApiKind && _openApi.Value is { } openApi)
        {
            await openApi.GenerateAsync(document.Name, writer, openApiSpecVersion: null);
        }
        else
        {
            var source = Sources().First(source => string.Equals(source.Kind, document.Kind, StringComparison.Ordinal)
                                                   && source.GetDocuments().Any(d => d.Name == document.Name));
            await source.GenerateAsync(document.Name, writer);
        }

        return writer.ToString();
    }

    private IEnumerable<IBuildTimeDocumentSource> Sources() => services.GetServices<IBuildTimeDocumentSource>();
}
