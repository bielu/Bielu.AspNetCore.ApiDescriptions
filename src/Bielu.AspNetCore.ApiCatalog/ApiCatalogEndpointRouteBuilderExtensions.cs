using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace Bielu.AspNetCore.ApiCatalog;

/// <summary>API catalog methods for <see cref="IEndpointRouteBuilder"/>.</summary>
public static class ApiCatalogEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Serves an RFC 9727 API catalog listing the API description documents this app serves: every document of
    /// <c>MapAsyncApi()</c> and <c>MapArazzo()</c>, those of <c>MapOpenApi()</c>, the endpoints marked with
    /// <see cref="ApiCatalogEndpointConventionBuilderExtensions.WithApiCatalogItem{TBuilder}"/> (such as an MCP
    /// server), and whatever <see cref="ApiCatalogOptions"/> adds.
    /// </summary>
    /// <remarks>
    /// <c>GET</c> returns the catalog as <see cref="ApiCatalogDefaults.MediaType"/>; <c>HEAD</c> returns only the headers.
    /// Both carry a <c>Link</c> header with the <c>api-catalog</c> relation, as the RFC requires. Links are absolute,
    /// built from the request's scheme, host and path base, so behind a proxy the forwarded headers have to be applied first.
    /// </remarks>
    /// <param name="endpoints">The <see cref="IEndpointRouteBuilder"/>.</param>
    /// <param name="pattern">The route to serve the catalog on. Defaults to <see cref="ApiCatalogDefaults.DefaultRoute"/>.</param>
    /// <returns>An <see cref="IEndpointConventionBuilder"/> to further customize the endpoint, e.g. <c>.RequireCors(...)</c>.</returns>
    /// <example>
    /// <code>
    /// app.MapOpenApi();
    /// app.MapAsyncApi();
    /// app.MapApiCatalog(); // GET /.well-known/api-catalog
    /// </code>
    /// </example>
    public static IEndpointConventionBuilder MapApiCatalog(this IEndpointRouteBuilder endpoints,
        [StringSyntax("Route")] string pattern = ApiCatalogDefaults.DefaultRoute)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);

        // A RequestDelegate rather than a parameter-bound lambda, which would go through RequestDelegateFactory's
        // reflection-based binding and fail under Native AOT.
        return endpoints.MapMethods(pattern, [HttpMethods.Get, HttpMethods.Head], (RequestDelegate)WriteCatalogAsync)
            .ExcludeFromDescription();
    }

    private static async Task WriteCatalogAsync(HttpContext context)
    {
        var request = context.Request;
        var baseUrl = $"{request.Scheme}://{request.Host}{request.PathBase}";
        var catalogUrl = baseUrl + request.Path;

        var options = context.RequestServices.GetRequiredService<IOptions<ApiCatalogOptions>>().Value;
        var payload = ApiCatalogWriter.Write(catalogUrl, baseUrl,
            ApiCatalogDocumentCollector.CollectServiceDescriptions(context.RequestServices, options), options,
            ApiCatalogDocumentCollector.CollectItems(context.RequestServices));

        context.Response.Headers[HeaderNames.Link] = $"<{catalogUrl}>; rel=\"api-catalog\"";
        context.Response.ContentType = ApiCatalogDefaults.MediaType;
        context.Response.ContentLength = payload.Length;

        if (HttpMethods.IsHead(request.Method))
        {
            return;
        }

        await context.Response.Body.WriteAsync(payload, context.RequestAborted);
    }
}
