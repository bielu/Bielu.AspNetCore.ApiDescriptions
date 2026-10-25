using Microsoft.AspNetCore.Builder;

namespace Bielu.AspNetCore.ApiCatalog;

/// <summary>API catalog conventions for endpoints.</summary>
public static class ApiCatalogEndpointConventionBuilderExtensions
{
    /// <summary>
    /// Lists the endpoint in the API catalog as an API of its own, an <c>item</c> link to its route. Use it for an API
    /// served from one endpoint that has no description document, such as a Model Context Protocol server.
    /// </summary>
    /// <remarks>
    /// Endpoints sharing a route, such as the <c>POST</c>, <c>GET</c> and <c>DELETE</c> endpoints of <c>MapMcp()</c>, are
    /// listed once. A route with parameters cannot be linked to and is left out.
    /// </remarks>
    /// <typeparam name="TBuilder">The endpoint convention builder type.</typeparam>
    /// <param name="builder">The endpoint, or group of endpoints, to list.</param>
    /// <param name="title">An optional human-readable title, for example <c>MCP server</c>.</param>
    /// <returns>The <paramref name="builder"/>, for chaining.</returns>
    /// <example>
    /// <code>
    /// app.MapMcp("/mcp").WithApiCatalogItem("MCP server");
    /// app.MapApiCatalog();
    /// </code>
    /// </example>
    public static TBuilder WithApiCatalogItem<TBuilder>(this TBuilder builder, string? title = null)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Add(endpoint => endpoint.Metadata.Add(new ApiCatalogItemMetadata(title)));
        return builder;
    }
}

/// <summary>Marks an endpoint to be listed in the API catalog as an <c>item</c>.</summary>
/// <param name="Title">The link's title, if any.</param>
internal sealed record ApiCatalogItemMetadata(string? Title);
