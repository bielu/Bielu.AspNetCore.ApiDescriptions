using Microsoft.Extensions.DependencyInjection;

namespace Bielu.AspNetCore.XRegistry;

/// <summary>Registers the xRegistry's options.</summary>
public static class XRegistryServiceCollectionExtensions
{
    /// <summary>
    /// Configures the xRegistry served by <c>MapXRegistry()</c>. Calling it is optional: without it the registry
    /// serves every AsyncAPI document with the default <see cref="XRegistryOptions"/>.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to register onto.</param>
    /// <param name="configure">Configures the <see cref="XRegistryOptions"/>.</param>
    /// <returns>The <paramref name="services"/>, for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.Services.AddXRegistry(options => options.WithRegistry("orders"));
    /// </code>
    /// </example>
    public static IServiceCollection AddXRegistry(this IServiceCollection services, Action<XRegistryOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var optionsBuilder = services.AddOptions<XRegistryOptions>();
        if (configure is not null)
        {
            optionsBuilder.Configure(configure);
        }

        return services;
    }
}
