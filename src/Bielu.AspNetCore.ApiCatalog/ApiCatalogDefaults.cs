namespace Bielu.AspNetCore.ApiCatalog;

/// <summary>Default values for the API catalog.</summary>
public static class ApiCatalogDefaults
{
    /// <summary>The well-known URI RFC 9727 registers for an API catalog.</summary>
    public const string DefaultRoute = "/.well-known/api-catalog";

    /// <summary>
    /// The media type the catalog is served with: an RFC 9264 Linkset carrying the RFC 9727 profile.
    /// </summary>
    public const string MediaType = "application/linkset+json; profile=\"" + Profile + "\"";

    /// <summary>The profile URI RFC 9727 defines for a Linkset that is an API catalog.</summary>
    public const string Profile = "https://www.rfc-editor.org/info/rfc9727";

    /// <summary>
    /// The route <c>Microsoft.AspNetCore.OpenApi</c>'s <c>MapOpenApi()</c> uses unless it is given another one.
    /// </summary>
    public const string DefaultOpenApiRoute = "/openapi/{documentName}.json";
}
