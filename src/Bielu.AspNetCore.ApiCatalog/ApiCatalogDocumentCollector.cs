using Bielu.AspNetCore.ApiDescriptions;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.ApiDescriptions;
using Microsoft.Extensions.DependencyInjection;

namespace Bielu.AspNetCore.ApiCatalog;

/// <summary>
/// Finds the API description documents the app serves, by looking at its endpoints: those marked with
/// <see cref="ApiDescriptionDocumentMetadata"/> (<c>MapAsyncApi()</c>, <c>MapArazzo()</c>) and the one mapped
/// on <see cref="ApiCatalogOptions.OpenApiRoutePattern"/>.
/// </summary>
internal static class ApiCatalogDocumentCollector
{
    internal const string OpenApiKind = "OpenAPI";
    private const string DocumentNameParameter = "documentName";

    /// <summary>
    /// Lists one <c>service-desc</c> link per document, with a path relative to the app's base URL, in the order
    /// the endpoints were mapped and, per endpoint, in document-name order.
    /// </summary>
    public static IReadOnlyList<ApiCatalogLink> CollectServiceDescriptions(IServiceProvider services, ApiCatalogOptions options)
    {
        var binderFactory = services.GetRequiredService<TemplateBinderFactory>();
        var sources = services.GetServices<IBuildTimeDocumentSource>().ToList();
        var links = new List<ApiCatalogLink>();

        foreach (var endpoint in services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>())
        {
            if (Describe(endpoint, options) is not var (kind, mediaType))
            {
                continue;
            }

            var binder = binderFactory.Create(endpoint.RoutePattern);
            if (endpoint.RoutePattern.GetParameter(DocumentNameParameter) is null)
            {
                if (binder.BindValues(new RouteValueDictionary()) is { } path)
                {
                    links.Add(new ApiCatalogLink(path, mediaType, $"{kind} document"));
                }

                continue;
            }

            var names = GetDocumentNames(kind, sources, services)
                .Select(name => name.ToLowerInvariant())
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal);
            foreach (var name in names)
            {
                if (binder.BindValues(new RouteValueDictionary { [DocumentNameParameter] = name }) is { } path)
                {
                    links.Add(new ApiCatalogLink(path, mediaType, $"{kind} document '{name}'"));
                }
            }
        }

        return links;
    }

    private static (string Kind, string MediaType)? Describe(RouteEndpoint endpoint, ApiCatalogOptions options)
    {
        if (endpoint.Metadata.GetMetadata<ApiDescriptionDocumentMetadata>() is { } metadata)
        {
            return (metadata.Kind, metadata.MediaType);
        }

        // MapOpenApi() leaves no metadata of its own on the endpoint, so it is recognized by its route.
        return options.OpenApiRoutePattern is { } openApiPattern
               && string.Equals(Normalize(endpoint.RoutePattern.RawText), Normalize(openApiPattern), StringComparison.OrdinalIgnoreCase)
            ? (OpenApiKind, IsYaml(openApiPattern) ? "application/vnd.oai.openapi+yaml" : "application/vnd.oai.openapi+json")
            : null;
    }

    private static IEnumerable<string> GetDocumentNames(string kind, List<IBuildTimeDocumentSource> sources,
        IServiceProvider services) =>
        kind == OpenApiKind
            ? OpenApiDocumentProviderAdapter.TryCreate(services)?.GetDocumentNames() ?? []
            : sources
                .Where(source => string.Equals(source.Kind, kind, StringComparison.OrdinalIgnoreCase))
                .SelectMany(source => source.GetDocuments().Select(document => document.Name));

    private static string Normalize(string? pattern) => pattern?.Trim('/') ?? string.Empty;

    private static bool IsYaml(string pattern) =>
        pattern.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) ||
        pattern.EndsWith(".yml", StringComparison.OrdinalIgnoreCase);
}
