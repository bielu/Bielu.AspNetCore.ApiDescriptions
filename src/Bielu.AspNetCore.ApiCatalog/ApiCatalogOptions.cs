namespace Bielu.AspNetCore.ApiCatalog;

/// <summary>Options for the API catalog served by <c>MapApiCatalog()</c>.</summary>
/// <remarks>
/// The documents served by <c>MapAsyncApi()</c> and <c>MapArazzo()</c> are found on their own. Use these
/// options for what the app cannot discover: the OpenAPI route when it is not the default, documentation
/// pages, and APIs hosted elsewhere.
/// </remarks>
/// <example>
/// <code>
/// builder.Services.AddApiCatalog(options => options
///     .AddServiceDoc("/scalar", "API reference")
///     .AddItem("https://payments.example.com/.well-known/api-catalog", "Payments"));
/// </code>
/// </example>
public sealed class ApiCatalogOptions
{
    /// <summary>
    /// The route <c>MapOpenApi()</c> was given, so its documents can be listed. Set it to <see langword="null"/>
    /// to leave OpenAPI documents out. Defaults to <see cref="ApiCatalogDefaults.DefaultOpenApiRoute"/>.
    /// </summary>
    /// <remarks>
    /// <c>MapOpenApi()</c> does not mark its endpoint, so the OpenAPI documents are listed only when an endpoint
    /// with exactly this route pattern is mapped.
    /// </remarks>
    public string? OpenApiRoutePattern { get; set; } = ApiCatalogDefaults.DefaultOpenApiRoute;

    internal List<ApiCatalogLink> Items { get; } = [];

    internal List<ApiCatalogLink> ServiceDocs { get; } = [];

    /// <summary>
    /// Adds an API to the catalog as an <c>item</c> link, for example another service or a nested catalog.
    /// </summary>
    /// <param name="href">The API's URL. A path such as <c>/v2</c> is resolved against the app's base URL.</param>
    /// <param name="title">An optional human-readable title.</param>
    /// <returns>The options, for chaining.</returns>
    public ApiCatalogOptions AddItem(string href, string? title = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(href);

        Items.Add(new ApiCatalogLink(href, Type: null, title));
        return this;
    }

    /// <summary>
    /// Adds human-readable documentation for this app's API as a <c>service-doc</c> link (RFC 8631), for
    /// example a Scalar page.
    /// </summary>
    /// <param name="href">The documentation's URL. A path such as <c>/scalar</c> is resolved against the app's base URL.</param>
    /// <param name="title">An optional human-readable title.</param>
    /// <returns>The options, for chaining.</returns>
    public ApiCatalogOptions AddServiceDoc(string href, string? title = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(href);

        ServiceDocs.Add(new ApiCatalogLink(href, "text/html", title));
        return this;
    }
}

/// <summary>A target link in the catalog.</summary>
/// <param name="Href">The target's URL, absolute or relative to the app's base URL.</param>
/// <param name="Type">The target's media type, if known.</param>
/// <param name="Title">The target's title, if any.</param>
internal sealed record ApiCatalogLink(string Href, string? Type, string? Title);
