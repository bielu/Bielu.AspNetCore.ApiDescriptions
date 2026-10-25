using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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

/// <summary>A document as generated, with what agents need to know about it before reading it whole.</summary>
/// <param name="Source">The document.</param>
/// <param name="Text">The document, serialized.</param>
/// <param name="Json">The document parsed, or <see langword="null"/> when it is not a JSON object.</param>
internal sealed record GeneratedApiDescription(ApiDescriptionDocument Source, string Text, JsonObject? Json)
{
    /// <summary>
    /// Identifies this exact content (<c>sha256:…</c>), so an agent can tell whether what it read before still holds.
    /// </summary>
    public string Fingerprint { get; } = "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Text)));

    /// <summary>The version of the specification the document follows: its <c>asyncapi</c>, <c>openapi</c> or <c>arazzo</c> field.</summary>
    public string? SpecificationVersion =>
        new[] { "asyncapi", "openapi", "arazzo", "swagger" }
            .Select(property => AsyncApiDocumentInspector.String(Json?[property]))
            .FirstOrDefault(version => version is not null);

    /// <summary>Which document this is and which version of it, for results that quote parts of it.</summary>
    public JsonObject Describe() => new()
    {
        ["kind"] = Source.Kind,
        ["name"] = Source.Name,
        ["uri"] = Source.Uri,
        ["specificationVersion"] = SpecificationVersion,
        ["fingerprint"] = Fingerprint,
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

    /// <summary>Generates a document and parses it.</summary>
    public async Task<GeneratedApiDescription> LoadAsync(ApiDescriptionDocument document)
    {
        var text = await GenerateAsync(document);
        JsonObject? json;
        try
        {
            json = JsonNode.Parse(text) as JsonObject;
        }
        catch (JsonException)
        {
            json = null;
        }

        return new GeneratedApiDescription(document, text, json);
    }

    private IEnumerable<IBuildTimeDocumentSource> Sources() => services.GetServices<IBuildTimeDocumentSource>();
}
