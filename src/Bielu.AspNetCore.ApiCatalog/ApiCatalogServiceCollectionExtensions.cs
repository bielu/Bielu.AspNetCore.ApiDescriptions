using Microsoft.Extensions.DependencyInjection;

namespace Bielu.AspNetCore.ApiCatalog;

/// <summary>Registers the API catalog's options.</summary>
public static class ApiCatalogServiceCollectionExtensions
{
    /// <summary>
    /// Configures the API catalog served by <c>MapApiCatalog()</c>. Calling it is optional: without it the
    /// catalog lists the documents it discovers with the default <see cref="ApiCatalogOptions"/>.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to register onto.</param>
    /// <param name="configure">Configures the <see cref="ApiCatalogOptions"/>.</param>
    /// <returns>The <paramref name="services"/>, for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.Services.AddApiCatalog(options => options.AddServiceDoc("/scalar"));
    /// </code>
    /// </example>
    public static IServiceCollection AddApiCatalog(this IServiceCollection services,
        Action<ApiCatalogOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var optionsBuilder = services.AddOptions<ApiCatalogOptions>();
        if (configure is not null)
        {
            optionsBuilder.Configure(configure);
        }

        return services;
    }
}
